param(
    [Parameter(Mandatory)][ValidateSet('PreTest','PostTest','Cleanup')][string]$Phase,
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [string]$TestOutputDirectory,
    [string]$AssetsPath,
    [string]$OutputRoot,
    [Parameter(Mandatory)][string]$PrivateDiagnosticDirectory,
    [string]$CollectorRequest
)
$ErrorActionPreference = 'Stop'
$maximumFileBytes = 64MB
$maximumTotalBytes = 256MB
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path


$privateDirectory = [IO.Path]::GetFullPath($PrivateDiagnosticDirectory)
function Assert-Child([string]$path, [string]$parent) {
    $full = [IO.Path]::GetFullPath($path)
    $prefix = [IO.Path]::GetFullPath($parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::Ordinal)) { throw 'Evidence path is outside its allowlisted parent.' }
    if (Test-Path -LiteralPath $parent) {
        $parentEntry = Get-Item -LiteralPath $parent -Force
        if (($parentEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Symbolic-link allowlisted parents are not allowed.' }
    }
    $cursor = $full
    while ($cursor.StartsWith($prefix, [StringComparison]::Ordinal)) {
        if (Test-Path -LiteralPath $cursor) {
            $entry = Get-Item -LiteralPath $cursor -Force
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Symbolic-link evidence paths are not allowed.' }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
}


Assert-Child $privateDirectory $env:RUNNER_TEMP
if ([IO.Path]::GetFileName($privateDirectory) -notmatch '^document-provenance-[A-Za-z0-9._-]+$') { throw 'Private diagnostic directory name is invalid.' }

function Get-BoundedFile([string]$path) {
    $file = Get-Item -LiteralPath $path -Force -ErrorAction Stop
    if ($file.PSIsContainer -or $file.Length -gt $maximumFileBytes -or $file.Length -le 0) { throw 'Evidence input is empty, oversized or not a file.' }
    return $file
}
function Get-GitIdentity([string]$path) {
    $commit = (& git -C $path rev-parse HEAD).Trim()
    $tree = (& git -C $path rev-parse 'HEAD^{tree}').Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$' -or $tree -notmatch '^[0-9a-f]{40}$') { throw 'Missing Git identity.' }
    return @{ commit = $commit; tree = $tree }
}
function Get-ApplicationMetadata([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            $definition = $reader.GetAssemblyDefinition()
            $module = $reader.GetModuleDefinition()
            $types = @($reader.TypeDefinitions | ForEach-Object {
                $type = $reader.GetTypeDefinition($_)
                @{ name = $reader.GetString($type.Name); namespace = $reader.GetString($type.Namespace); attributes = $type.Attributes.ToString(); methods = @($type.GetMethods()).Count; fields = @($type.GetFields()).Count }
            })
            $methods = @($reader.MethodDefinitions | ForEach-Object {
                $method = $reader.GetMethodDefinition($_)
                $bodyHash = $null
                if ($method.RelativeVirtualAddress -ne 0) {
                    $body = [System.Reflection.Metadata.PEReaderExtensions]::GetMethodBody($pe, $method.RelativeVirtualAddress)
                    $bodyHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($body.GetILBytes()))
                }
                @{ name = $reader.GetString($method.Name); attributes = $method.Attributes.ToString(); implementation = $method.ImplAttributes.ToString(); rva = $method.RelativeVirtualAddress; signature = [Convert]::ToHexString($reader.GetBlobBytes($method.Signature)); ilSha256 = $bodyHash }
            })
            return @{ name = $reader.GetString($definition.Name); version = $definition.Version.ToString(); culture = $reader.GetString($definition.Culture); publicKey = [Convert]::ToHexString($reader.GetBlobBytes($definition.PublicKey)); mvid = $reader.GetGuid($module.Mvid).ToString(); entryPoint = $pe.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress; nativeHeaderSize = $pe.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size; flags = $pe.PEHeaders.CorHeader.Flags.ToString(); types = $types; methods = $methods; fields = $reader.FieldDefinitions.Count }
        } finally { $pe.Dispose() }
    } finally { $stream.Dispose() }
}
function Remove-OwnedPrivateDiagnostics {
    Assert-Child $privateDirectory $env:RUNNER_TEMP
    if (-not (Test-Path -LiteralPath $privateDirectory)) { return }
    $markerPath = Join-Path $privateDirectory 'owner.json'
    Assert-Child $markerPath $privateDirectory
    $markerFile = Get-BoundedFile $markerPath
    if ($markerFile.Length -gt 4096) { throw 'Oversized private ownership marker.' }
    $marker = Get-Content -LiteralPath $markerFile.FullName -Raw | ConvertFrom-Json
    if ($marker.run -ne $env:GITHUB_RUN_ID -or $marker.attempt -ne $env:GITHUB_RUN_ATTEMPT) { throw 'Private diagnostic ownership mismatch.' }
    foreach ($entry in @(Get-ChildItem -LiteralPath $privateDirectory -Recurse -Force)) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Private diagnostics contain a symbolic link; cleanup refused.' }
    }
    Remove-Item -LiteralPath $privateDirectory -Recurse -Force
}
if ($Phase -eq 'Cleanup') {
    Remove-OwnedPrivateDiagnostics
    Write-Host '[document-provenance] Owned private diagnostics cleaned.'
    exit 0
}
if ($env:GITHUB_EVENT_NAME -ne 'pull_request' -or $env:GITHUB_REPOSITORY -ne 'MALIEV-Co-Ltd/Legacy.Maliev.DocumentService') { throw 'Evidence capture requires the owned hosted PR event.' }
$testDirectory = (Resolve-Path -LiteralPath $TestOutputDirectory).Path
$workspaceOutput = [IO.Path]::GetFullPath($OutputRoot)
Assert-Child $testDirectory $repository
Assert-Child $workspaceOutput (Join-Path $repository 'runner-results')
if ($CollectorRequest -ne 'XPlat Code Coverage') { throw 'Unexpected requested collector; no coverage configuration changes permitted.' }
[System.Reflection.Assembly]::Load('System.Reflection.Metadata') | Out-Null
$assemblies = @('Legacy.Maliev.DocumentService.Application','Legacy.Maliev.DocumentService.Api','Legacy.Maliev.DocumentService.Domain')
$privateOwned = $false
$captureSucceeded = $false
$ownerMarker = Join-Path $privateDirectory 'owner.json'
if ($Phase -eq 'PostTest' -and (Test-Path -LiteralPath $privateDirectory)) {
    Assert-Child $ownerMarker $privateDirectory
    $ownerFile = Get-BoundedFile $ownerMarker
    if ($ownerFile.Length -gt 4096) { throw 'Oversized private ownership marker.' }
    $owner = Get-Content -LiteralPath $ownerFile.FullName -Raw | ConvertFrom-Json
    if ($owner.run -ne $env:GITHUB_RUN_ID -or $owner.attempt -ne $env:GITHUB_RUN_ATTEMPT) { throw 'Private diagnostic ownership mismatch.' }
    $privateOwned = $true
}
try {
$phaseDirectory = Join-Path $workspaceOutput $Phase
if (Test-Path -LiteralPath $phaseDirectory) { throw 'Phase already captured; duplicate target invocation is a setup failure.' }
[void][IO.Directory]::CreateDirectory($phaseDirectory)
$totalBytes = 0L
$snapshots = @()
foreach ($assembly in $assemblies) {
    foreach ($location in @('production','test-copy')) {
        $directory = if ($location -eq 'production') { Join-Path $repository "$assembly/bin/Release/net10.0" } else { $testDirectory }
        foreach ($extension in @('dll','pdb')) {
            $path = Join-Path $directory "$assembly.$extension"
            Assert-Child $path $repository
            $file = Get-BoundedFile $path
            $totalBytes += $file.Length
            if ($totalBytes -gt $maximumTotalBytes) { throw 'Evidence total byte limit exceeded.' }
            $name = "$location-$assembly.$extension"
            $sourceHashBefore = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $copyPath = Join-Path $phaseDirectory $name
            Copy-Item -LiteralPath $file.FullName -Destination $copyPath
            $copyHash = (Get-FileHash -LiteralPath $copyPath -Algorithm SHA256).Hash
            if ($copyHash -ne $sourceHashBefore -or $copyHash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw 'Evidence input changed during snapshot.' }
            $snapshots += @{ identity = "$location/$assembly.$extension"; sha256 = $copyHash; bytes = (Get-Item -LiteralPath $copyPath).Length; artifact = "$Phase/$name" }
        }
    }
}
$application = Get-ApplicationMetadata (Join-Path $phaseDirectory 'test-copy-Legacy.Maliev.DocumentService.Application.dll')
$identity = Get-GitIdentity $repository
if ($env:GITHUB_RUN_ID -notmatch '^\d+$' -or $env:GITHUB_RUN_ATTEMPT -notmatch '^\d+$') { throw 'Missing hosted run identity.' }
$receipt = [ordered]@{ schemaVersion = 1; phase = $Phase; timestampUtc = [DateTime]::UtcNow.ToString('O'); candidate = $null; checkout = $identity; run = $env:GITHUB_RUN_ID; attempt = $env:GITHUB_RUN_ATTEMPT; testTarget = [IO.Path]::GetRelativePath($repository, $testDirectory); collectorRequest = $CollectorRequest; snapshots = $snapshots; application = $application; dependencyPins = @(); collectorFiles = @(); collectorLoadEvidence = @(); effectiveSettingsVerified = $false; transformationVerified = $false; evidenceComplete = $false; policyActive = $false; runtimeAccepted = $false; rawNumericalPassed = $false }
$expectedDependencies = @{
    'Legacy.Maliev.ServiceDefaults' = '8f4f5f27b226ffe406c4c79b1903742e8c2e7dd3'
    'Legacy.Maliev.CompatibilityContracts' = '78e48ffc4ee000df0510cba5e7c7a3c4c4d539d7'
}
foreach ($dependency in $expectedDependencies.Keys) {
    $dependencyIdentity = Get-GitIdentity (Join-Path $repository ".dependencies/$dependency")
    if ($dependencyIdentity.commit -ne $expectedDependencies[$dependency]) { throw 'Frozen dependency commit mismatch.' }
    $receipt.dependencyPins += @{ name = $dependency; identity = $dependencyIdentity }
}
$eventFile = Get-BoundedFile $env:GITHUB_EVENT_PATH
$event = Get-Content -LiteralPath $eventFile.FullName -Raw | ConvertFrom-Json
if ($event.pull_request.head.sha -match '^[0-9a-f]{40}$') { $receipt.candidate = $event.pull_request.head.sha } else { throw 'Missing PR candidate identity.' }
if ($Phase -eq 'PreTest') {
    if ($application.methods.Count -ne 5 -or $application.fields -ne 0 -or @($application.methods | Where-Object rva -ne 0).Count -ne 0) { throw 'PreTest capture was not before Application instrumentation.' }
    $assetsFile = Get-BoundedFile $AssetsPath
    Assert-Child $assetsFile.FullName $repository
    $assets = Get-Content -LiteralPath $assetsFile.FullName -Raw | ConvertFrom-Json
    $library = $assets.libraries.'coverlet.collector/6.0.4'
    if ($null -eq $library -or $library.type -ne 'package') { throw 'Pinned collector package resolution absent.' }
    $packageRoots = @($assets.packageFolders.PSObject.Properties.Name)
    if ($packageRoots.Count -ne 1) { throw 'Ambiguous NuGet package root.' }
    $packageRoot = Join-Path $packageRoots[0] $library.path
    Assert-Child $packageRoot $packageRoots[0]
    $packagePaths = @('coverlet.collector.6.0.4.nupkg','coverlet.collector.6.0.4.nupkg.sha512','build/netstandard2.0/coverlet.collector.dll','build/netstandard2.0/coverlet.core.dll','build/netstandard2.0/Mono.Cecil.dll')
    foreach ($relative in $packagePaths) {
        $packagePath = Join-Path $packageRoot $relative
        $file = Get-BoundedFile $packagePath
        $totalBytes += $file.Length
        if ($totalBytes -gt $maximumTotalBytes) { throw 'Evidence total byte limit exceeded.' }
        Assert-Child $file.FullName $packageRoot
        $name = [IO.Path]::GetFileName($relative)
        $sourceHashBefore = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $copyPath = Join-Path $phaseDirectory $name
        Copy-Item -LiteralPath $file.FullName -Destination $copyPath
        $copyHash = (Get-FileHash -LiteralPath $copyPath -Algorithm SHA256).Hash
        if ($copyHash -ne $sourceHashBefore -or $copyHash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw 'Evidence input changed during snapshot.' }
        $receipt.collectorFiles += @{ packageRelativePath = $relative; sha256 = $copyHash; bytes = (Get-Item -LiteralPath $copyPath).Length; artifact = "$Phase/$name" }
    }
    $nupkg = Join-Path $phaseDirectory 'coverlet.collector.6.0.4.nupkg'
    $packageHash = [Convert]::ToBase64String([Convert]::FromHexString((Get-FileHash -LiteralPath $nupkg -Algorithm SHA512).Hash))
    $diskHash = (Get-Content -LiteralPath (Join-Path $phaseDirectory 'coverlet.collector.6.0.4.nupkg.sha512') -Raw).Trim()
    if ($packageHash -ne $diskHash) { throw 'Collector package byte hash mismatch.' }
    # NuGet assets uses the signature-excluding content hash, distinct from signed archive bytes.
    $metadataPath = Join-Path $packageRoot '.nupkg.metadata'
    Assert-Child $metadataPath $packageRoot
    $metadataFile = Get-BoundedFile $metadataPath
    if ($metadataFile.Length -gt 4096) { throw 'Oversized NuGet content hash metadata.' }
    $totalBytes += $metadataFile.Length
    if ($totalBytes -gt $maximumTotalBytes) { throw 'Evidence total byte limit exceeded.' }
    $metadata = Get-Content -LiteralPath $metadataFile.FullName -Raw | ConvertFrom-Json
    if ($library.sha512 -notmatch '^[A-Za-z0-9+/]{86}==$' -or $metadata.contentHash -notmatch '^[A-Za-z0-9+/]{86}==$') { throw 'Invalid NuGet content hash metadata.' }
    try {
        $assetsContentBytes = [Convert]::FromBase64String($library.sha512)
        $metadataContentBytes = [Convert]::FromBase64String($metadata.contentHash)
    } catch { throw 'Invalid NuGet content hash metadata.' }
    if ($assetsContentBytes.Length -ne 64 -or $metadataContentBytes.Length -ne 64 -or $library.sha512 -ne $metadata.contentHash) { throw 'Collector NuGet content hash metadata mismatch.' }
    $receipt.packageBytesSha512 = $packageHash
    $receipt.assetsContentSha512 = $library.sha512
    $receipt.nugetMetadataContentSha512 = $metadata.contentHash
    $receipt.nugetContentHashMetadataJoinVerified = $true
    $receipt.packageSha512Verified = $true
    $receipt.resolvedCollectorPackage = 'coverlet.collector/6.0.4'
    if (Test-Path -LiteralPath $privateDirectory) { throw 'Private diagnostics path already exists.' }
    [void][IO.Directory]::CreateDirectory($privateDirectory)
    $privateOwned = $true
    Assert-Child $privateDirectory $env:RUNNER_TEMP
    if (-not [OperatingSystem]::IsWindows()) {
        [IO.File]::SetUnixFileMode($privateDirectory, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
        if ([IO.File]::GetUnixFileMode($privateDirectory) -ne ([IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)) { throw 'Private diagnostic mode must be owner-only.' }
    }
    @{ run = $env:GITHUB_RUN_ID; attempt = $env:GITHUB_RUN_ATTEMPT } | ConvertTo-Json | Set-Content -LiteralPath $ownerMarker -Encoding utf8
} else {
    $pre = Get-Content -LiteralPath (Join-Path $workspaceOutput 'PreTest/receipt.json') -Raw | ConvertFrom-Json
    if ($pre.checkout.commit -ne $identity.commit -or $pre.candidate -ne $receipt.candidate -or $pre.run -ne $receipt.run -or $pre.attempt -ne $receipt.attempt) { throw 'Pre/post provenance mismatch.' }
    foreach ($snapshot in $snapshots) {
        $original = @($pre.snapshots | Where-Object identity -eq $snapshot.identity)
        if ($original.Count -ne 1 -or $original[0].sha256 -ne $snapshot.sha256) { throw 'Pristine/restored snapshot mismatch.' }
    }
    $receipt.restoredEqualsPreTest = $true
    # Diagnostic mentions alone are not proof that a module executed. Keep selection/settings incomplete.
    try {
        $diagnostics = @(Get-ChildItem -LiteralPath $privateDirectory -File | Where-Object Name -ne 'owner.json')
        if ($diagnostics.Count -gt 16) { throw 'Diagnostic file count limit exceeded.' }
        $diagnosticTotalBytes = 0L
        foreach ($diagnostic in $diagnostics) {
            Assert-Child $diagnostic.FullName $privateDirectory
            $file = Get-BoundedFile $diagnostic.FullName
            $diagnosticTotalBytes += $file.Length
            $totalBytes += $file.Length
            if ($totalBytes -gt $maximumTotalBytes) { throw 'Evidence total byte limit exceeded.' }
            if ($file.Length -gt 8MB -or $diagnosticTotalBytes -gt 32MB) { throw 'Diagnostic byte limit exceeded.' }
            $mentions = @(Get-Content -LiteralPath $file.FullName | Where-Object { $_ -match 'coverlet\.(collector|core)\.dll|Mono\.Cecil\.dll' })
            foreach ($module in @('coverlet.collector.dll','coverlet.core.dll','Mono.Cecil.dll')) {
                $count = @($mentions | Where-Object { $_.Contains($module, [StringComparison]::OrdinalIgnoreCase) }).Count
                if ($count -gt 0) { $receipt.collectorLoadEvidence += @{ module = $module; diagnosticMentionCount = $count; executionAttributed = $false } }
            }
        }
        $observedPath = Join-Path (Split-Path $workspaceOutput -Parent) 'document-runtime-application.dll'
        Assert-Child $observedPath (Join-Path $repository 'runner-results')
        $observed = Get-BoundedFile $observedPath
        $totalBytes += $observed.Length
        if ($totalBytes -gt $maximumTotalBytes) { throw 'Evidence total byte limit exceeded.' }
        $observedApplication = Get-ApplicationMetadata $observed.FullName
        $receipt.observedApplication = $observedApplication
        $receipt.observedSha256 = (Get-FileHash -LiteralPath $observed.FullName -Algorithm SHA256).Hash
        $receipt.observedHasExecutableMethods = @($observedApplication.methods | Where-Object rva -ne 0).Count -gt 0
        if (-not $receipt.observedHasExecutableMethods) { throw 'Expected real collector observation missing.' }
    } finally {
        # Only delete this exact owned, validated private directory; raw text is never uploaded or echoed.
        Assert-Child $privateDirectory $env:RUNNER_TEMP
        Remove-OwnedPrivateDiagnostics
    }
}
$receipt.remainingObligations = @('Executed collector/core selection not proven by diagnostic mentions','Effective collector settings incomplete','Normalized IL/member transformation manifest not verified','Complete portable-PDB/source/consumer/raw join remains required')
$receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $phaseDirectory 'receipt.json') -Encoding utf8
$captureSucceeded = $true
Write-Host "[document-provenance] $Phase captured; applicability remains inactive and incomplete."
} finally {
    if ($privateOwned -and ($Phase -eq 'PostTest' -or -not $captureSucceeded)) {
        Assert-Child $privateDirectory $env:RUNNER_TEMP
        Remove-OwnedPrivateDiagnostics
    }
}
