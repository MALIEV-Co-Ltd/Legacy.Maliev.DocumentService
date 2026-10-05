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
    private static readonly MethodInfo[] Profile = typeof(IDocumentRenderer).GetMethods();

    [Theory]
    [InlineData("interface", "exact-profile")]
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
    public void CompiledProfile_ReportsIntendedReasonWithoutAcceptingIncompleteEvidence(string fixture, string expectedReason)
    {
        using var image = EmitFixture(fixture);
        Assert.InRange(image.Length, 1, 1024 * 1024);
        using var pe = new PEReader(image);
        var reader = pe.GetMetadataReader();
        Assert.InRange(reader.MethodDefinitions.Count, 4, 6);
        Assert.InRange(reader.TypeDefinitions.Count, 2, 4);
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
            if (method.Attributes != (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract)
                || method.GetGenericParameters().Count != 0 || !SignatureMatches(reader, method, expected)) return new(false, "signature-profile");
        }
        return new(true, "exact-profile");
    }

    private static bool IsExternal(MethodDefinition method) => (method.Attributes & MethodAttributes.PinvokeImpl) != 0
        || (method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL
        || (method.ImplAttributes & (MethodImplAttributes.InternalCall | MethodImplAttributes.Unmanaged | MethodImplAttributes.ForwardRef)) != 0;

    private static bool SignatureMatches(MetadataReader reader, MethodDefinition method, MethodInfo expected)
    {
        var blob = reader.GetBlobReader(method.Signature);
        var header = blob.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method || header.CallingConvention != SignatureCallingConvention.Default
            || !header.IsInstance || header.IsGeneric || header.HasExplicitThis || blob.ReadCompressedInteger() != 1
            || blob.ReadSignatureTypeCode() != SignatureTypeCode.SZArray || blob.ReadSignatureTypeCode() != SignatureTypeCode.Byte
            || blob.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle) return false;
        var handle = blob.ReadTypeHandle();
        if (handle.Kind != HandleKind.TypeReference || blob.RemainingBytes != 0) return false;
        var type = reader.GetTypeReference((TypeReferenceHandle)handle);
        var expectedType = expected.GetParameters()[0].ParameterType;
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
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("Document.StructuralProposalFixture"), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("Fixture");
        var contract = module.DefineType(fixture == "wrong-interface" ? "Fixture.WrongInterface" : typeof(IDocumentRenderer).FullName!, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        for (var index = 0; index < Profile.Length; index++)
        {
            if (fixture == "missing-member" && index == 0) continue;
            var expected = Profile[index];
            var parameter = expected.GetParameters()[0].ParameterType;
            if (index == 0 && fixture == "wrong-parameter") parameter = typeof(string);
            if (index == 0 && fixture == "wrong-assembly")
            {
                var spoof = new PersistedAssemblyBuilder(new AssemblyName("Untrusted.Domain"), typeof(object).Assembly);
                parameter = spoof.DefineDynamicModule("Spoof").DefineType(parameter.FullName!, TypeAttributes.Public).CreateType()!;
            }
            var attributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Abstract;
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
