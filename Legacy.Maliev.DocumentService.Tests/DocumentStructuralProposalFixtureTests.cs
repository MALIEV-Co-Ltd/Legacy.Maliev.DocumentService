using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Xunit.Abstractions;
using Legacy.Maliev.DocumentService.Application;

namespace Legacy.Maliev.DocumentService.Tests;

// Inactive shape proposal only: synthetic targets are never loaded or invoked.
public sealed class DocumentStructuralProposalFixtureTests(ITestOutputHelper output)
{
    private sealed record ContractMethod(string Name, Type ParameterType);
    private static readonly ContractMethod[] Profile =
    [
        new("RenderInvoice", typeof(Legacy.Maliev.DocumentService.Domain.Invoice.Invoice)),
        new("RenderPurchaseOrder", typeof(Legacy.Maliev.DocumentService.Domain.PurchaseOrder.PurchaseOrder)),
        new("RenderQuotation", typeof(Legacy.Maliev.DocumentService.Domain.Quotations.Quotation)),
        new("RenderReceipt", typeof(Legacy.Maliev.DocumentService.Domain.Receipt.Receipt)),
        new("RenderOrderLabel", typeof(Legacy.Maliev.DocumentService.Domain.OrderLabel.OrderLabel))
    ];

    [Theory]
    [InlineData("interface", "exact-profile")]
    [InlineData("wrong-definition-identity", "assembly-profile")]
    [InlineData("extra-member", "member-profile")]
    [InlineData("default-body", "managed-body")]
    [InlineData("hidden-helper", "managed-body")]
    [InlineData("generated-accessor", "managed-body")]
    [InlineData("module-initializer", "managed-body")]
    [InlineData("collector-named-helper", "managed-body")]
    [InlineData("runtime-zero-rva", "external-implementation")]
    [InlineData("native-zero-rva", "external-implementation")]
    [InlineData("internal-call-zero-rva", "external-implementation")]
    [InlineData("forward-ref-zero-rva", "external-implementation")]
    [InlineData("pinvoke-zero-rva", "external-implementation")]
    [InlineData("field", "field")]
    [InlineData("initialized-field", "field")]
    [InlineData("entrypoint", "entrypoint")]
    [InlineData("wrong-interface", "type-profile")]
    [InlineData("extra-type", "type-profile")]
    [InlineData("missing-member", "member-profile")]
    [InlineData("wrong-name", "member-profile")]
    [InlineData("wrong-return", "signature-profile")]
    [InlineData("wrong-parameter", "signature-profile")]
    [InlineData("wrong-assembly", "signature-profile")]
    [InlineData("wrong-assembly-version", "signature-profile")]
    [InlineData("wrong-assembly-culture", "signature-profile")]
    [InlineData("wrong-assembly-token", "signature-profile")]
    public void CompiledProfile_ReportsIntendedReasonWithoutAcceptingIncompleteEvidence(string fixture, string expectedReason)
    {
        var actualProfile = typeof(IDocumentRenderer).GetMethods();
        Assert.Equal(5, actualProfile.Length);
        foreach (var expected in Profile)
        {
            var actual = Assert.Single(actualProfile, item => item.Name == expected.Name);
            Assert.Equal(typeof(byte[]), actual.ReturnType);
            Assert.Equal(expected.ParameterType, Assert.Single(actual.GetParameters()).ParameterType);
        }
        using var image = EmitFixture(fixture);
        Assert.InRange(image.Length, 1, 1024 * 1024);
        using var pe = new PEReader(image);
        var reader = pe.GetMetadataReader();
        Assert.InRange(reader.MethodDefinitions.Count, 4, 6);
        Assert.InRange(reader.TypeDefinitions.Count, 2, 4);
        AssertFixtureFault(fixture, pe, reader);
        var result = Evaluate(pe, reader);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { fixture, sha256 = Convert.ToHexString(SHA256.HashData(image.ToArray())), methods = reader.MethodDefinitions.Count, fields = reader.FieldDefinitions.Count, shapeVerified = result.ShapeVerified, evidenceComplete = result.EvidenceComplete, structuralDispositionProposed = result.StructuralDispositionProposed, policyActive = result.PolicyActive, runtimeAccepted = result.RuntimeAccepted, rawNumericalPassed = result.RawNumericalPassed, reason = result.Reason }));
        Assert.Equal(expectedReason, result.Reason);
        Assert.Equal(fixture == "interface", result.ShapeVerified);
        Assert.False(result.EvidenceComplete);
        Assert.Equal(result.ShapeVerified ? "shape-only-evidence-incomplete" : "rejected", result.StructuralDispositionProposed);
        Assert.False(result.PolicyActive);
        Assert.False(result.RuntimeAccepted);
        Assert.False(result.RawNumericalPassed);
        // A reason is reached only after inspecting real PE metadata; setup exceptions fail the test.
        if (fixture.EndsWith("zero-rva", StringComparison.Ordinal))
        {
            Assert.Contains(reader.MethodDefinitions, handle =>
            {
                var method = reader.GetMethodDefinition(handle);
                return method.RelativeVirtualAddress == 0 && IsExternal(method);
            });
        }
        if (fixture == "module-initializer")
        {
            var module = reader.GetTypeDefinition(reader.TypeDefinitions.First());
            Assert.Contains(module.GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == ".cctor"
                && reader.GetMethodDefinition(handle).RelativeVirtualAddress > 0);
        }
        if (fixture == "pinvoke-zero-rva")
        {
            var imported = Assert.Single(reader.MethodDefinitions.Select(reader.GetMethodDefinition), method => (method.Attributes & MethodAttributes.PinvokeImpl) != 0);
            Assert.Equal("FixtureImport", reader.GetString(imported.GetImport().Name));
        }
    }

    [Fact]
    public void CompiledRuntimeNativeHeader_IsRejectedWithoutInvokingTargetCode()
    {
        // Hosted runtime CoreLib is a genuine compiled ReadyToRun artifact, not a forged PE header.
        var path = typeof(object).Assembly.Location;
        using var stream = File.OpenRead(path);
        Assert.InRange(stream.Length, 1, 64 * 1024 * 1024);
        using var pe = new PEReader(stream);
        var directory = pe.PEHeaders.CorHeader!.ManagedNativeHeaderDirectory;
        Assert.True(directory.Size >= 16, "Hosted runtime must provide a valid ReadyToRun native-header control; absence is a fixture setup failure.");
        var nativeHeader = pe.GetSectionData(directory.RelativeVirtualAddress).GetReader(0, directory.Size);
        Assert.Equal(0x00525452u, nativeHeader.ReadUInt32());
        var result = Evaluate(pe, pe.GetMetadataReader());
        Assert.Equal("native-header", result.Reason);
        Assert.False(result.ShapeVerified);
        Assert.False(result.EvidenceComplete);
        Assert.False(result.PolicyActive);
        Assert.False(result.RuntimeAccepted);
        Assert.False(result.RawNumericalPassed);
        stream.Position = 0;
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { fixture = "compiled-runtime-native-header", sha256 = Convert.ToHexString(SHA256.HashData(stream)), nativeHeaderSize = directory.Size, reason = result.Reason, shapeVerified = result.ShapeVerified, evidenceComplete = result.EvidenceComplete, structuralDispositionProposed = result.StructuralDispositionProposed, policyActive = result.PolicyActive, runtimeAccepted = result.RuntimeAccepted, rawNumericalPassed = result.RawNumericalPassed }));
    }

    private static void AssertFixtureFault(string fixture, PEReader pe, MetadataReader reader)
    {
        var methods = reader.MethodDefinitions.Select(reader.GetMethodDefinition).ToArray();
        var names = methods.Select(method => reader.GetString(method.Name)).ToArray();
        if (fixture == "wrong-definition-identity") Assert.Equal("Untrusted.Product", reader.GetString(reader.GetAssemblyDefinition().Name));
        if (fixture == "missing-member") { Assert.Equal(4, methods.Length); Assert.DoesNotContain(Profile[0].Name, names); }
        if (fixture == "extra-member") { Assert.Equal(6, methods.Length); Assert.Contains("Extra", names); }
        if (fixture == "wrong-name") { Assert.Contains("WrongName", names); Assert.DoesNotContain(Profile[0].Name, names); }
        if (fixture == "field") Assert.Single(reader.FieldDefinitions);
        if (fixture == "initialized-field") Assert.Contains(reader.FieldDefinitions, handle => reader.GetFieldDefinition(handle).GetRelativeVirtualAddress() != 0);
        if (fixture == "entrypoint") Assert.NotEqual(0, pe.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress);
        if (fixture == "wrong-interface") Assert.Contains(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Name) == "WrongInterface");
        if (fixture == "extra-type") Assert.Equal(3, reader.TypeDefinitions.Count);
        if (fixture is "wrong-return" or "wrong-parameter" or "wrong-assembly" or "wrong-assembly-version" or "wrong-assembly-culture" or "wrong-assembly-token")
        {
            var method = Assert.Single(methods, item => reader.GetString(item.Name) == Profile[0].Name);
            var blob = reader.GetBlobReader(method.Signature);
            Assert.True(blob.ReadSignatureHeader().IsInstance);
            Assert.Equal(1, blob.ReadCompressedInteger());
            if (fixture == "wrong-return") Assert.Equal(SignatureTypeCode.Void, blob.ReadSignatureTypeCode());
            else
            {
                Assert.Equal(SignatureTypeCode.SZArray, blob.ReadSignatureTypeCode());
                Assert.Equal(SignatureTypeCode.Byte, blob.ReadSignatureTypeCode());
                if (fixture == "wrong-parameter") Assert.Equal(SignatureTypeCode.String, blob.ReadSignatureTypeCode());
                else
                {
                    Assert.Equal(0x12, blob.ReadByte());
                    var type = reader.GetTypeReference((TypeReferenceHandle)blob.ReadTypeHandle());
                    Assert.Equal(Profile[0].ParameterType.FullName, reader.GetString(type.Namespace) + "." + reader.GetString(type.Name));
                    var reference = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
                    var expectedIdentity = Profile[0].ParameterType.Assembly.GetName();
                    Assert.Equal(fixture == "wrong-assembly" ? "Untrusted.Domain" : expectedIdentity.Name, reader.GetString(reference.Name));
                    Assert.Equal(fixture == "wrong-assembly-version" ? new Version(9, 8, 7, 6) : expectedIdentity.Version, reference.Version);
                    Assert.Equal(fixture == "wrong-assembly-culture" ? "th-TH" : expectedIdentity.CultureName ?? "", reader.GetString(reference.Culture));
                    Assert.Equal(fixture == "wrong-assembly-token" ? new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 } : expectedIdentity.GetPublicKeyToken() ?? [], reader.GetBlobBytes(reference.PublicKeyOrToken));
                }
            }
        }
        if (fixture is "default-body" or "hidden-helper" or "generated-accessor" or "collector-named-helper")
        {
            var expectedName = fixture == "default-body" ? Profile[0].Name : fixture == "generated-accessor" ? "get_Value" : "Execute";
            Assert.Contains(methods, method => reader.GetString(method.Name) == expectedName && method.RelativeVirtualAddress > 0);
            if (fixture == "collector-named-helper") Assert.Contains(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "Coverlet.Core.Instrumentation.Tracker");
        }
        var expectedFlag = fixture switch
        {
            "runtime-zero-rva" => MethodImplAttributes.Runtime,
            "native-zero-rva" => MethodImplAttributes.Native,
            "internal-call-zero-rva" => MethodImplAttributes.InternalCall,
            "forward-ref-zero-rva" => MethodImplAttributes.ForwardRef,
            _ => (MethodImplAttributes)0
        };
        if (expectedFlag != 0) Assert.Contains(methods, method => method.RelativeVirtualAddress == 0 && (method.ImplAttributes & expectedFlag) == expectedFlag);
    }

    private sealed record Proposal(bool ShapeVerified, string Reason)
    {
        public bool EvidenceComplete => false;
        public string StructuralDispositionProposed => ShapeVerified ? "shape-only-evidence-incomplete" : "rejected";
        public bool PolicyActive => false;
        public bool RuntimeAccepted => false;
        public bool RawNumericalPassed => false;
    }

    private static Proposal Evaluate(PEReader pe, MetadataReader reader)
    {

        var header = pe.PEHeaders.CorHeader;
        if (header is null || header.EntryPointTokenOrRelativeVirtualAddress != 0) return new(false, "entrypoint");
        if ((header.Flags & CorFlags.ILOnly) == 0 || header.ManagedNativeHeaderDirectory.Size != 0) return new(false, "native-header");
        var identity = reader.GetAssemblyDefinition();
        if (reader.GetString(identity.Name) != "Document.StructuralProposalFixture"
            || identity.Version != new Version(1, 0, 0, 0) || reader.GetString(identity.Culture) != ""
            || reader.GetBlobBytes(identity.PublicKey).Length != 0) return new(false, "assembly-profile");
        var methods = reader.MethodDefinitions.Select(reader.GetMethodDefinition).ToArray();
        if (methods.Any(IsExternal)) return new(false, "external-implementation");
        if (methods.Any(method => method.RelativeVirtualAddress != 0)) return new(false, "managed-body");
        if (reader.FieldDefinitions.Count != 0) return new(false, "field");
        var types = reader.TypeDefinitions.Select(reader.GetTypeDefinition).ToArray();
        if (types.Length != 2 || reader.GetString(types[0].Name) != "<Module>" || types[0].GetMethods().Count != 0
            || types[0].GetFields().Count != 0 || reader.GetString(types[1].Name) != "IDocumentRenderer"
            || reader.GetString(types[1].Namespace) != typeof(IDocumentRenderer).Namespace
            || types[1].Attributes != (TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract)) return new(false, "type-profile");
        if (methods.Length != Profile.Length || !methods.Select(method => reader.GetString(method.Name)).Order().SequenceEqual(Profile.Select(method => method.Name).Order())) return new(false, "member-profile");
        foreach (var method in methods)
        {
            var expected = Profile.Single(item => item.Name == reader.GetString(method.Name));
            if (method.Attributes != (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract | MethodAttributes.HideBySig)
                || method.GetGenericParameters().Count != 0 || !SignatureMatches(reader, method, expected)) return new(false, "signature-profile");
        }
        return new(true, "exact-profile");
    }

    private static bool IsExternal(MethodDefinition method) => (method.Attributes & MethodAttributes.PinvokeImpl) != 0
        || (method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL
        || (method.ImplAttributes & (MethodImplAttributes.InternalCall | MethodImplAttributes.Unmanaged | MethodImplAttributes.ForwardRef)) != 0;

    private static bool SignatureMatches(MetadataReader reader, MethodDefinition method, ContractMethod expected)
    {
        var blob = reader.GetBlobReader(method.Signature);
        var header = blob.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method || header.CallingConvention != SignatureCallingConvention.Default
            || !header.IsInstance || header.IsGeneric || header.HasExplicitThis || blob.ReadCompressedInteger() != 1
            || blob.ReadSignatureTypeCode() != SignatureTypeCode.SZArray || blob.ReadSignatureTypeCode() != SignatureTypeCode.Byte
            || blob.ReadByte() != 0x12) return false;
        var handle = blob.ReadTypeHandle();
        if (handle.Kind != HandleKind.TypeReference || blob.RemainingBytes != 0) return false;
        var type = reader.GetTypeReference((TypeReferenceHandle)handle);
        var expectedType = expected.ParameterType;
        if (reader.GetString(type.Name) != expectedType.Name || reader.GetString(type.Namespace) != expectedType.Namespace
            || type.ResolutionScope.Kind != HandleKind.AssemblyReference) return false;
        var reference = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
        var identity = expectedType.Assembly.GetName();
        return reader.GetString(reference.Name) == identity.Name && reference.Version == identity.Version
            && reader.GetString(reference.Culture) == (identity.CultureName ?? "")
            && (reference.Flags & AssemblyFlags.PublicKey) == 0
            && reader.GetBlobBytes(reference.PublicKeyOrToken).SequenceEqual(identity.GetPublicKeyToken() ?? []);
    }

    private static MemoryStream EmitFixture(string fixture)
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName(fixture == "wrong-definition-identity" ? "Untrusted.Product, Version=1.0.0.0" : "Document.StructuralProposalFixture, Version=1.0.0.0"), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("Fixture");
        var contract = module.DefineType(fixture == "wrong-interface" ? "Fixture.WrongInterface" : typeof(IDocumentRenderer).FullName!, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        for (var index = 0; index < Profile.Length; index++)
        {
            if (fixture == "missing-member" && index == 0) continue;
            var expected = Profile[index];
            var parameter = expected.ParameterType;
            if (index == 0 && fixture == "wrong-parameter") parameter = typeof(string);
            if (index == 0 && (fixture is "wrong-assembly" or "wrong-assembly-version" or "wrong-assembly-culture" or "wrong-assembly-token"))
            {
                var spoofIdentity = new AssemblyName(parameter.Assembly.GetName().FullName);
                if (fixture == "wrong-assembly") spoofIdentity.Name = "Untrusted.Domain";
                if (fixture == "wrong-assembly-version") spoofIdentity.Version = new Version(9, 8, 7, 6);
                if (fixture == "wrong-assembly-culture") spoofIdentity.CultureName = "th-TH";
                if (fixture == "wrong-assembly-token") spoofIdentity.SetPublicKeyToken([1, 2, 3, 4, 5, 6, 7, 8]);
                var spoof = new PersistedAssemblyBuilder(spoofIdentity, typeof(object).Assembly);
                parameter = spoof.DefineDynamicModule("Spoof").DefineType(parameter.FullName!, TypeAttributes.Public).CreateType()!;
            }
            var attributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract | MethodAttributes.HideBySig;
            if (index == 0 && fixture == "default-body") attributes &= ~MethodAttributes.Abstract;
            var method = contract.DefineMethod(index == 0 && fixture == "wrong-name" ? "WrongName" : expected.Name, attributes,
                index == 0 && fixture == "wrong-return" ? typeof(void) : typeof(byte[]), [parameter]);
            if (index == 0)
            {
                if (fixture == "default-body") { method.GetILGenerator().Emit(OpCodes.Ldnull); method.GetILGenerator().Emit(OpCodes.Ret); }
                if (fixture == "runtime-zero-rva") method.SetImplementationFlags(MethodImplAttributes.Runtime);
                if (fixture == "native-zero-rva") method.SetImplementationFlags(MethodImplAttributes.Native);
                if (fixture == "internal-call-zero-rva") method.SetImplementationFlags(MethodImplAttributes.InternalCall);
                if (fixture == "forward-ref-zero-rva") method.SetImplementationFlags(MethodImplAttributes.ForwardRef);
            }
        }
        if (fixture == "field") contract.DefineField("Untrusted", typeof(int), FieldAttributes.Public | FieldAttributes.Static);
        if (fixture == "extra-member") contract.DefineMethod("Extra", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract | MethodAttributes.HideBySig, typeof(byte[]), [Profile[0].ParameterType]);
        contract.CreateType();
        if (fixture == "initialized-field") module.DefineInitializedData("Data", [1, 2, 3, 4], FieldAttributes.Public | FieldAttributes.Static);
        if (fixture is "hidden-helper" or "generated-accessor" or "collector-named-helper" or "pinvoke-zero-rva" or "entrypoint" or "extra-type")
        {
            var helper = module.DefineType(fixture == "collector-named-helper" ? "Coverlet.Core.Instrumentation.Tracker.UntrustedHelper" : "Fixture.HiddenHelper", TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);
            if (fixture == "pinvoke-zero-rva") helper.DefinePInvokeMethod("Import", "fixture-library.invalid", "FixtureImport", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl,
                CallingConventions.Standard, typeof(void), Type.EmptyTypes, CallingConvention.Cdecl, CharSet.Ansi);
            else if (fixture != "extra-type") helper.DefineMethod(fixture == "generated-accessor" ? "get_Value" : "Execute", MethodAttributes.Private | MethodAttributes.Static, typeof(void), Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
            helper.CreateType();
        }
        if (fixture == "module-initializer") module.DefineGlobalMethod(".cctor", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, typeof(void), Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
        module.CreateGlobalFunctions();
        var stream = new MemoryStream();
        assembly.Save(stream);
        if (fixture == "entrypoint")
        {
            stream.Position = 0;
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var reader = pe.GetMetadataReader();
            var entry = reader.MethodDefinitions.Single(handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == "Execute");
            // Mutate the emitted CLI entry-point field to a real static void method token.
            stream.Position = pe.PEHeaders.CorHeaderStartOffset + 20;
            stream.Write(BitConverter.GetBytes(MetadataTokens.GetToken(entry)));
        }
        stream.Position = 0;
        return stream;
    }
}
