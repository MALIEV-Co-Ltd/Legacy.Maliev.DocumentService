using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Legacy.Maliev.DocumentService.Tests;

// Proposal fixtures only. No production coverage decision consumes this prototype.
// Synthetic PE bytes are inspected, never loaded or invoked; no generated helper is exercised.
public sealed class DocumentStructuralProposalFixtureTests
{
    [Theory]
    [InlineData("interface", true)]
    [InlineData("default-body", false)]
    [InlineData("hidden-helper", false)]
    [InlineData("module-initializer", false)]
    [InlineData("runtime-zero-rva", false)]
    [InlineData("internal-call-zero-rva", false)]
    [InlineData("pinvoke-zero-rva", false)]
    [InlineData("collector-named-helper", false)]
    public void CompiledShapePrototype_RejectsEveryExecutableOrExternallyImplementedFixture(
        string fixture, bool expectedShape)
    {
        using var image = EmitFixture(fixture);
        using var pe = new PEReader(image);
        var reader = pe.GetMetadataReader();
        var types = reader.TypeDefinitions.Select(reader.GetTypeDefinition).ToArray();
        var methods = reader.MethodDefinitions.Select(reader.GetMethodDefinition).ToArray();
        var nonExecutableShape = pe.PEHeaders.CorHeader is { } header
            && header.EntryPointTokenOrRelativeVirtualAddress == 0
            && (header.Flags & CorFlags.ILOnly) != 0
            && header.ManagedNativeHeaderDirectory.Size == 0
            && types.All(type => reader.GetString(type.Name) == "<Module>"
                || (type.Attributes & TypeAttributes.Interface) != 0)
            && types.All(type => !type.GetFields().Any())
            && methods.All(method => method.RelativeVirtualAddress == 0
                && (method.Attributes & MethodAttributes.Abstract) != 0
                && (method.Attributes & MethodAttributes.PinvokeImpl) == 0
                && (method.ImplAttributes & MethodImplAttributes.CodeTypeMask) == MethodImplAttributes.IL
                && (method.ImplAttributes & (MethodImplAttributes.InternalCall
                    | MethodImplAttributes.Unmanaged | MethodImplAttributes.ForwardRef)) == 0);

        Assert.Equal(expectedShape, nonExecutableShape);
        if (fixture == "runtime-zero-rva")
        {
            Assert.Contains(methods, method => method.RelativeVirtualAddress == 0
                && (method.ImplAttributes & MethodImplAttributes.CodeTypeMask) == MethodImplAttributes.Runtime);
        }
        if (fixture == "internal-call-zero-rva")
        {
            Assert.Contains(methods, method => method.RelativeVirtualAddress == 0
                && (method.ImplAttributes & MethodImplAttributes.InternalCall) != 0);
        }
        if (fixture == "pinvoke-zero-rva")
        {
            var imported = Assert.Single(methods, method => (method.Attributes & MethodAttributes.PinvokeImpl) != 0);
            Assert.Equal(0, imported.RelativeVirtualAddress);
            Assert.Equal("FixtureImport", reader.GetString(imported.GetImport().Name));
        }
        if (fixture == "module-initializer")
        {
            var module = Assert.Single(types, type => reader.GetString(type.Name) == "<Module>");
            Assert.Contains(module.GetMethods(), handle => reader.GetMethodDefinition(handle).RelativeVirtualAddress != 0);
        }
    }

    private static MemoryStream EmitFixture(string fixture)
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("Document.StructuralProposalFixture"), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("Fixture");
        var contract = module.DefineType("Fixture.IContract", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var method = contract.DefineMethod("Operation", MethodAttributes.Public | MethodAttributes.Virtual
            | MethodAttributes.NewSlot | MethodAttributes.Abstract, typeof(void), Type.EmptyTypes);
        if (fixture == "default-body")
        {
            var implementation = contract.DefineMethod("DefaultOperation", MethodAttributes.Public | MethodAttributes.Virtual
                | MethodAttributes.NewSlot, typeof(void), Type.EmptyTypes);
            implementation.GetILGenerator().Emit(OpCodes.Ret);
        }
        if (fixture == "runtime-zero-rva")
        {
            method.SetImplementationFlags(MethodImplAttributes.Runtime);
        }
        if (fixture == "internal-call-zero-rva")
        {
            method.SetImplementationFlags(MethodImplAttributes.InternalCall);
        }
        contract.CreateType();

        if (fixture is "hidden-helper" or "collector-named-helper" or "pinvoke-zero-rva")
        {
            var name = fixture == "collector-named-helper"
                ? "Coverlet.Core.Instrumentation.Tracker.UntrustedHelper"
                : "Fixture.HiddenHelper";
            var helper = module.DefineType(name, TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);
            if (fixture == "pinvoke-zero-rva")
            {
                helper.DefinePInvokeMethod("Import", "fixture-library.invalid", "FixtureImport",
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl,
                    CallingConventions.Standard, typeof(void), Type.EmptyTypes,
                    CallingConvention.Cdecl, CharSet.Ansi);
            }
            else
            {
                helper.DefineMethod("Execute", MethodAttributes.Private | MethodAttributes.Static,
                    typeof(void), Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
            }
            helper.CreateType();
        }
        if (fixture == "module-initializer")
        {
            var initializer = module.DefineGlobalMethod(".cctor", MethodAttributes.Private | MethodAttributes.Static
                | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, typeof(void), Type.EmptyTypes);
            initializer.GetILGenerator().Emit(OpCodes.Ret);
            module.CreateGlobalFunctions();
        }
        var stream = new MemoryStream();
        assembly.Save(stream);
        stream.Position = 0;
        return stream;
    }
}
