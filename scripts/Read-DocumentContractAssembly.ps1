param(
    [Parameter(Mandatory)][string]$DllPath,
    [Parameter(Mandatory)][string]$PdbPath,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedHead,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputPath) { throw 'Refusing to replace previous proof.' }
$stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $DllPath).Path)
$symbols = [IO.File]::OpenRead((Resolve-Path -LiteralPath $PdbPath).Path)
$pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
$provider = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($symbols)
try {
    $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    $debug = $provider.GetMetadataReader()
    $definition = $reader.GetAssemblyDefinition()
    $assemblyName = 'Legacy.Maliev.DocumentService.Application'
    if ($reader.GetString($definition.Name) -cne $assemblyName -or $definition.Version.ToString() -ne '1.0.0.0' -or
        $reader.GetString($definition.Culture) -ne '' -or $reader.GetBlobBytes($definition.PublicKey).Length -ne 0) {
        throw 'Unexpected assembly identity.'
    }
    if ([int]$pe.PEHeaders.CorHeader.Flags -ne 1 -or $pe.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress -ne 0 -or
        $pe.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size -ne 0 -or $pe.PEHeaders.CorHeader.ResourcesDirectory.Size -ne 0) {
        throw 'Executable entry point, native image or resources present.'
    }
    $tables = [ordered]@{}
    $allowed = @('Module', 'TypeRef', 'TypeDef', 'MethodDef', 'Param', 'MemberRef', 'CustomAttribute', 'Assembly', 'AssemblyRef')
    foreach ($table in [Enum]::GetValues([System.Reflection.Metadata.Ecma335.TableIndex])) {
        $count = [System.Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetTableRowCount($reader, $table)
        $tables[$table.ToString()] = $count
        if ($count -ne 0 -and $table.ToString() -notin $allowed) { throw "Forbidden metadata table: $table" }
    }
    if ($tables.Module -ne 1 -or $tables.TypeDef -ne 2 -or $tables.MethodDef -ne 5 -or $tables.Param -ne 5 -or $tables.Assembly -ne 1) {
        throw 'Only the module and five-method interface are applicable.'
    }
    $expected = [ordered]@{
        RenderInvoice = @('Invoice.Invoice', 'invoice')
        RenderPurchaseOrder = @('PurchaseOrder.PurchaseOrder', 'purchaseOrder')
        RenderQuotation = @('Quotations.Quotation', 'quotation')
        RenderReceipt = @('Receipt.Receipt', 'receipt')
        RenderOrderLabel = @('OrderLabel.OrderLabel', 'orderLabel')
    }
    $methods = @()
    foreach ($handle in $reader.TypeDefinitions) {
        $type = $reader.GetTypeDefinition($handle)
        $name = $reader.GetString($type.Name)
        if ($name -ceq '<Module>') {
            if ([int]$type.Attributes -ne 0 -or $reader.GetString($type.Namespace) -ne '' -or
                @($type.GetMethods()).Count -ne 0 -or @($type.GetFields()).Count -ne 0 -or -not $type.BaseType.IsNil) {
                throw 'Unexpected module implementation.'
            }
            continue
        }
        if ($name -cne 'IDocumentRenderer' -or $reader.GetString($type.Namespace) -cne $assemblyName -or
            [int]$type.Attributes -ne 1048737 -or -not $type.BaseType.IsNil -or @($type.GetFields()).Count -ne 0 -or
            @($type.GetInterfaceImplementations()).Count -ne 0 -or @($type.GetMethods()).Count -ne 5) {
            throw 'Unexpected concrete, generated, inherited or additional type.'
        }
        foreach ($methodHandle in $type.GetMethods()) {
            $method = $reader.GetMethodDefinition($methodHandle)
            $methodName = $reader.GetString($method.Name)
            if ($methodName -cnotin $expected.Keys -or $methodName -cin $methods.name -or
                [int]$method.Attributes -ne 1478 -or [int]$method.ImplAttributes -ne 0 -or $method.RelativeVirtualAddress -ne 0) {
                throw 'Unexpected concrete, default, static or duplicate method.'
            }
            $blob = $reader.GetBlobReader($method.Signature)
            if ($blob.ReadByte() -ne 0x20 -or $blob.ReadCompressedInteger() -ne 1 -or
                $blob.ReadByte() -ne 0x1d -or $blob.ReadByte() -ne 0x05 -or $blob.ReadByte() -ne 0x12) {
                throw 'Unexpected method calling convention or return/parameter type.'
            }
            $coded = $blob.ReadCompressedInteger()
            if (($coded -band 3) -ne 1 -or $blob.RemainingBytes -ne 0) { throw 'Unexpected signature encoding.' }
            $reference = $reader.GetTypeReference([System.Reflection.Metadata.Ecma335.MetadataTokens]::TypeReferenceHandle($coded -shr 2))
            $parameterType = $reader.GetString($reference.Namespace) + '.' + $reader.GetString($reference.Name)
            if ($parameterType -cne ('Legacy.Maliev.DocumentService.Domain.' + $expected[$methodName][0]) -or
                $reference.ResolutionScope.Kind.ToString() -ne 'AssemblyReference') { throw 'Unexpected Domain parameter.' }
            $scope = $reader.GetAssemblyReference([System.Reflection.Metadata.AssemblyReferenceHandle]$reference.ResolutionScope)
            if ($reader.GetString($scope.Name) -cne 'Legacy.Maliev.DocumentService.Domain' -or
                $scope.Version.ToString() -ne '1.0.0.0' -or $reader.GetString($scope.Culture) -ne '' -or
                $reader.GetBlobBytes($scope.PublicKeyOrToken).Length -ne 0) { throw 'Unexpected parameter assembly.' }
            $parameters = @($method.GetParameters())
            if ($parameters.Count -ne 1) { throw 'Unexpected parameter count.' }
            $parameter = $reader.GetParameter($parameters[0])
            if ($parameter.SequenceNumber -ne 1 -or [int]$parameter.Attributes -ne 0 -or
                $reader.GetString($parameter.Name) -cne $expected[$methodName][1]) { throw 'Unexpected parameter contract.' }
            $methods += [ordered]@{ name = $methodName; parameter = $parameterType; rva = 0 }
        }
    }
    if ($methods.Count -ne 5) { throw 'Incomplete interface contract.' }
    $information = @()
    foreach ($attrHandle in $definition.GetCustomAttributes()) {
        $attr = $reader.GetCustomAttribute($attrHandle)
        if ($attr.Constructor.Kind.ToString() -ne 'MemberReference') { throw 'Unexpected attribute constructor.' }
        $ctor = $reader.GetMemberReference([System.Reflection.Metadata.MemberReferenceHandle]$attr.Constructor)
        if ($ctor.Parent.Kind.ToString() -ne 'TypeReference') { throw 'Unexpected attribute type.' }
        $attrType = $reader.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$ctor.Parent)
        if ($reader.GetString($attrType.Namespace) -ceq 'System.Reflection' -and
            $reader.GetString($attrType.Name) -ceq 'AssemblyInformationalVersionAttribute') {
            $value = $reader.GetBlobReader($attr.Value)
            if ($value.ReadUInt16() -ne 1) { throw 'Invalid informational-version attribute.' }
            $information += $value.ReadSerializedString()
            if ($value.ReadUInt16() -ne 0 -or $value.RemainingBytes -ne 0) { throw 'Invalid informational-version payload.' }
        }
    }
    if ($information.Count -ne 1 -or $information[0] -cne "1.0.0+$ExpectedHead") { throw 'Compiled candidate SHA mismatch.' }
    $sequencePoints = 0
    foreach ($handle in $debug.MethodDebugInformation) {
        $sequencePoints += @($debug.GetMethodDebugInformation($handle).GetSequencePoints()).Count
    }
    if ($sequencePoints -ne 0) { throw 'Executable sequence points present.' }
    $codeViews = @($pe.ReadDebugDirectory() | Where-Object Type -eq CodeView)
    if ($codeViews.Count -ne 1) { throw 'Expected one CodeView identity.' }
    $codeView = $pe.ReadCodeViewDebugDirectoryData($codeViews[0])
    $identity = [byte[]]$debug.DebugMetadataHeader.Id
    $expectedIdentity = $codeView.Guid.ToByteArray() + [BitConverter]::GetBytes([uint32]$codeViews[0].Stamp)
    if ([Convert]::ToHexString($identity) -cne [Convert]::ToHexString($expectedIdentity) -or $codeView.Age -ne 1) {
        throw 'DLL and portable PDB identity mismatch.'
    }
    [ordered]@{
        schemaVersion = 'document-contract-surface/v1'; policyActive = $false; runtimeAccepted = $false
        assembly = $assemblyName; compiledHead = $ExpectedHead; informationalVersion = $information[0]
        dllSha256 = (Get-FileHash -LiteralPath $DllPath).Hash.ToLowerInvariant()
        pdbSha256 = (Get-FileHash -LiteralPath $PdbPath).Hash.ToLowerInvariant()
        pdbIdentity = [Convert]::ToHexString($identity); methods = $methods; tables = $tables
        sequencePoints = 0; numericalPercent = $null; numericalPassed = $false
    } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
} finally {
    $provider.Dispose()
    $pe.Dispose()
    $symbols.Dispose()
    $stream.Dispose()
}
