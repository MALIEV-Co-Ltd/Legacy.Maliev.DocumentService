param([ValidateSet('Library','Watch')][string]$Mode='Library',[string]$PrivateDirectory)
# Passive Linux process observation only. Mapped bytes are not proof that a method executed.
# No raw diagnostics, command line, maps, unknown paths or exception text leave the private root.
$documentProducerNames=@('datacollector.dll','Microsoft.TestPlatform.Common.dll','Microsoft.TestPlatform.CoreUtilities.dll','Microsoft.TestPlatform.PlatformAbstractions.dll','Microsoft.VisualStudio.TestPlatform.ObjectModel.dll')
function Assert-DocumentProducerPath([string]$Path,[string]$Root) {
    $full=[IO.Path]::GetFullPath($Path);$parent=[IO.Path]::GetFullPath($Root)
    if(-not $full.StartsWith($parent.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::Ordinal)){throw 'Producer path outside fixed root.'}
    $cursor=$full
    while($cursor){
        $item=if([IO.Directory]::Exists($cursor)){[IO.DirectoryInfo]::new($cursor)}else{[IO.FileInfo]::new($cursor)}
        if($null -ne $item.LinkTarget -or ($item.Exists -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint))){throw 'Linked producer path refused.'}
        $cursor=[IO.Path]::GetDirectoryName($cursor)
    }
    return $full
}
function Read-DocumentProducerBytes([string]$Path,[string]$Root,[long]$Maximum=64MB) {
    $full=Assert-DocumentProducerPath $Path $Root
    $stream=[IO.FileStream]::new($full,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    try {
        if($stream.Length -le 0 -or $stream.Length -gt $Maximum){throw 'Producer input byte bound exceeded.'}
        $bytes=[byte[]]::new([int]$stream.Length);$stream.ReadExactly($bytes)
        if($stream.ReadByte() -ne -1){throw 'Producer input grew during read.'}
        [void](Assert-DocumentProducerPath $full $Root)
        return ,$bytes
    } finally {$stream.Dispose()}
}
function Write-DocumentProducerBytes([string]$Path,[string]$Root,[byte[]]$Bytes) {
    $full=Assert-DocumentProducerPath $Path $Root
    $stream=[IO.FileStream]::new($full,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {$stream.Write($Bytes);$stream.Flush()} finally {$stream.Dispose()}
    [void](Assert-DocumentProducerPath $full $Root)
}
function Read-DocumentProducerOwner([string]$Root) {
    [void](Assert-DocumentProducerPath $Root $env:RUNNER_TEMP)
    $bytes=Read-DocumentProducerBytes (Join-Path $Root 'owner.json') $Root 4096
    $owner=[Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xfeff)|ConvertFrom-Json
    if($owner.run -cne $env:GITHUB_RUN_ID -or $owner.attempt -cne $env:GITHUB_RUN_ATTEMPT -or $owner.run -cnotmatch '^[0-9]+$' -or $owner.attempt -cnotmatch '^[0-9]+$'){throw 'Producer private ownership mismatch.'}
}
function ConvertFrom-DocumentProducerStat([string]$Text,[string]$ExpectedProcessId) {
    # comm can contain spaces and parentheses; all following stat fields are numeric/state.
    if(-not ($Text -cmatch '^([0-9]+) \(.*\) ([A-Z]) (.+)$') -or $Matches[1] -cne $ExpectedProcessId){throw 'Producer process stat identity invalid.'}
    $fields=$Matches[3].Trim().Split(' ',[StringSplitOptions]::RemoveEmptyEntries)
    if($fields.Count -lt 19 -or $fields[18] -cnotmatch '^[0-9]+$'){throw 'Producer process start identity absent.'}
    return $fields[18]
}
function Read-DocumentProducerEpoch([string]$ProcessId) {
    if($ProcessId -cnotmatch '^[1-9][0-9]*$'){throw 'Invalid producer PID.'}
    # /proc is a kernel surface, not a regular snapshot allowlist. Bound each read separately.
    $bytes=Read-DocumentProducerKernelBytes "/proc/$ProcessId/stat" 4096
    return ConvertFrom-DocumentProducerStat ([Text.Encoding]::UTF8.GetString($bytes).TrimEnd()) $ProcessId
}
function Read-DocumentProducerKernelBytes([string]$Path,[int]$Maximum) {
    if($Path -cnotmatch '^/proc/[1-9][0-9]*/(stat|status|cmdline|maps)$' -or $Maximum -le 0 -or $Maximum -gt 4MB){throw 'Unexpected process surface.'}
    $stream=[IO.File]::OpenRead($Path);$output=[IO.MemoryStream]::new()
    try {
        $buffer=[byte[]]::new(4096)
        while(($count=$stream.Read($buffer,0,[Math]::Min($buffer.Length,$Maximum+1-[int]$output.Length))) -gt 0){
            $output.Write($buffer,0,$count)
            if($output.Length -gt $Maximum){throw 'Process surface byte bound exceeded.'}
        }
        if($output.Length -eq 0){throw 'Empty process surface.'}
        return ,$output.ToArray()
    } finally {$output.Dispose();$stream.Dispose()}
}
function Get-DocumentProducerUid([string]$ProcessId) {
    $text=[Text.Encoding]::UTF8.GetString((Read-DocumentProducerKernelBytes "/proc/$ProcessId/status" 64KB))
    return ConvertFrom-DocumentProducerUid $text
}
function ConvertFrom-DocumentProducerUid([string]$Text) {
    if($Text.Length -gt 64KB){throw 'Process user status bound exceeded.'}
    if($Text -cmatch '(?m)^Uid:\s+([0-9]+)\s+([0-9]+)\s+([0-9]+)\s+([0-9]+)\s*$' -and $Matches[1] -ceq $Matches[2] -and $Matches[1] -ceq $Matches[3] -and $Matches[1] -ceq $Matches[4]){return $Matches[1]}
    throw 'Process user identity unavailable or changed.'
}
function Assert-DocumentObserverCommand([byte[]]$Bytes,[string]$Root,[string]$PwshPath,[string]$ScriptPath) {
    if($Bytes.Length -gt 16384){throw 'Observer command byte limit exceeded.'}
    $arguments=[Text.UTF8Encoding]::new($false,$true).GetString($Bytes).Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)
    $expected=@($PwshPath,'-NoLogo','-NoProfile','-NonInteractive','-File',$ScriptPath,'-Mode','Watch','-PrivateDirectory',$Root)
    if($arguments.Count -ne $expected.Count){throw 'Observer process role mismatch.'}
    for($index=0;$index -lt $expected.Count;$index++){if($arguments[$index] -cne $expected[$index]){throw 'Observer process role mismatch.'}}
}
function Assert-DocumentObserverProcess([string]$ProcessId,[string]$Epoch,[string]$Root) {
    $pwsh=Join-Path $PSHOME 'pwsh';$scriptPath=Join-Path $PSScriptRoot 'Watch-DocumentTraceProducer.ps1'
    Assert-DocumentObserverIdentity (Read-DocumentProducerEpoch $ProcessId) $Epoch (Get-DocumentProducerUid $ProcessId) (Get-DocumentProducerUid $PID.ToString()) ([IO.FileInfo]::new("/proc/$ProcessId/exe").LinkTarget) $pwsh (Read-DocumentProducerKernelBytes "/proc/$ProcessId/cmdline" 16384) $Root $scriptPath
    if((Read-DocumentProducerEpoch $ProcessId) -cne $Epoch){throw 'Observer process epoch changed.'}
}
function Assert-DocumentObserverIdentity([string]$Epoch,[string]$ExpectedEpoch,[string]$UserId,[string]$ExpectedUserId,[string]$Executable,[string]$PwshPath,[byte[]]$Command,[string]$Root,[string]$ScriptPath) {
    if($Epoch -cnotmatch '^[0-9]+$' -or $ExpectedEpoch -cnotmatch '^[0-9]+$' -or $Epoch -cne $ExpectedEpoch){throw 'Observer process epoch mismatch.'}
    if($UserId -cnotmatch '^[0-9]+$' -or $ExpectedUserId -cnotmatch '^[0-9]+$' -or $UserId -cne $ExpectedUserId){throw 'Observer process user mismatch.'}
    if($Executable -cne $PwshPath){throw 'Observer executable mismatch.'}
    Assert-DocumentObserverCommand $Command $Root $PwshPath $ScriptPath
}
function ConvertFrom-DocumentProducerCommand([byte[]]$Bytes,[string]$DotnetRoot) {
    if($Bytes.Length -gt 16384){throw 'Producer command bound exceeded.'}
    $arguments=[Text.UTF8Encoding]::new($false,$true).GetString($Bytes).Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)
    if($DotnetRoot -cnotmatch '^/(?:[^/\\]+/)*[^/\\]+/?$' -or @($DotnetRoot.Split('/')|Where-Object {$_ -cin @('.','..')}).Count){throw 'Invalid producer SDK root.'}
    $root=$DotnetRoot.TrimEnd('/')
    if($arguments.Count -lt 2 -or $arguments.Count -gt 64 -or $arguments[0] -cne "$root/dotnet"){throw 'Unexpected managed producer launcher.'}
    $index=if($arguments[1] -ceq 'exec'){2}else{1}
    if($arguments.Count -le $index -or -not ($arguments[$index] -cmatch ('^'+[regex]::Escape($root)+'/sdk/([0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?)/Extensions/datacollector\.dll$'))){throw 'Unexpected producer SDK entry point.'}
    $sdk=$Matches[1]
    return @{sdkVersion=$sdk;entryPoint=$arguments[$index];sdkDirectory="$root/sdk/$sdk"}
}
function ConvertFrom-DocumentProducerMap([string]$Line,[string]$SdkDirectory) {
    if($Line.Length -gt 32768){throw 'Producer map line bound exceeded.'}
    if(-not ($Line -cmatch '^([0-9a-f]+)-([0-9a-f]+) ([r-][w-][x-][ps]) ([0-9a-f]+) ([0-9a-f]+):([0-9a-f]+) ([0-9]+) +(.*)$')){return $null}
    $map=@{}+$Matches;$path=$map[8]
    $name=[IO.Path]::GetFileName($path)
    if($name -cnotin $documentProducerNames){return $null}
    if($path -cne "$SdkDirectory/Extensions/$name" -or $map[7] -eq '0' -or -not $map[3].StartsWith('r',[StringComparison]::Ordinal)){throw 'Producer mapping scope invalid.'}
    return @{module=$name;path=$path;inode=$map[7];deviceMajor=[Convert]::ToUInt64($map[5],16);deviceMinor=[Convert]::ToUInt64($map[6],16)}
}
function Add-DocumentProducerMap([hashtable]$Maps,[hashtable]$Map) {
    if($Maps.ContainsKey($Map.module) -and ($Maps[$Map.module].inode -cne $Map.inode -or $Maps[$Map.module].deviceMajor -ne $Map.deviceMajor -or $Maps[$Map.module].deviceMinor -ne $Map.deviceMinor)){throw 'Ambiguous module inode/device.'}
    $Maps[$Map.module]=$Map
}
function Get-DocumentProducerRefusalCategory([string]$Message) {
    # Whitelisted literal comparisons only; arbitrary exception messages are never exported.
    switch -CaseSensitive ($Message) {
        'Unexpected managed producer launcher.' {return 'managed-launcher'}
        'Unexpected producer SDK entry point.' {return 'sdk-entry-point'}
        'Producer executable launcher mismatch.' {return 'executable-launcher'}
        'Collector process user mismatch.' {return 'process-user'}
        'Producer path outside fixed root.' {return 'path-scope'}
        'Linked producer path refused.' {return 'linked-path'}
        'Producer private ownership mismatch.' {return 'private-owner'}
        'Observer startup registration invalid.' {return 'startup-registration'}
        'Mapped file inode/device mismatch.' {return 'mapped-inode-device'}
        'Producer inode process timed out.' {return 'stat-timeout'}
        'Producer metadata module mismatch.' {return 'module-identity'}
        'Producer bytes changed during snapshot.' {return 'snapshot-byte-mismatch'}
        'Producer mapping scope invalid.' {return 'mapped-path-scope'}
        'Multiple collector processes; no unique producer binding.' {return 'multiple-processes'}
        'Producer total read budget exceeded.' {return 'read-budget'}
        'Producer aggregate read budget exceeded.' {return 'read-budget'}
        'Producer input byte bound exceeded.' {return 'input-byte-bound'}
        'Producer input grew during read.' {return 'input-growth'}
        default {return 'operation-refused'}
    }
}
function Get-DocumentProducerInode([string]$Path) {
    $start=[Diagnostics.ProcessStartInfo]::new('/usr/bin/stat');$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @('--dereference','--format=%d:%i:%s','--',$Path)){$start.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($start)
    try {
        if(-not $process.WaitForExit(1000)){throw 'Producer inode process timed out.'}
        $buffer=[char[]]::new(4097)
        $count=$process.StandardOutput.ReadBlock($buffer,0,$buffer.Length)
        if($count -gt 4096){throw 'Producer inode output limit exceeded.'}
        $output=[string]::new($buffer,0,$count)
        $errorCount=$process.StandardError.ReadBlock($buffer,0,$buffer.Length)
        if($errorCount -gt 4096 -or $process.ExitCode -ne 0 -or -not ($output -cmatch '^([0-9]+):([0-9]+):([0-9]+)\s*$')){throw 'Producer inode unavailable.'}
        $device=[UInt64]$Matches[1]
        return @{inode=$Matches[2];bytes=[long]$Matches[3];major=(($device -shr 8) -band 4095) -bor (($device -shr 32) -band 0xfffff000);minor=($device -band 255) -bor (($device -shr 12) -band 0xffffff00)}
    } finally {if(-not $process.HasExited){$process.Kill();[void]$process.WaitForExit(1000)};$process.Dispose()}
}
function Get-DocumentProducerMetadata([byte[]]$Bytes,[string]$Module) {
    $stream=[IO.MemoryStream]::new($Bytes,$false)
    try {
        $pe=[Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $reader=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe);$assembly=$reader.GetAssemblyDefinition();$definition=$reader.GetModuleDefinition()
            if(($reader.GetString($assembly.Name)+'.dll') -cne $Module){throw 'Producer metadata module mismatch.'}
            $culture=$reader.GetString($assembly.Culture)
            if($culture -cnotin @('','neutral')){throw 'Unexpected producer culture.'}
            $key=$reader.GetBlobBytes($assembly.PublicKey);$token='null'
            if($key.Length){$hash=[Security.Cryptography.SHA1]::HashData($key);$suffix=[byte[]]$hash[($hash.Length-8)..($hash.Length-1)];[Array]::Reverse($suffix);$token=[Convert]::ToHexString($suffix).ToLowerInvariant()}
            $debug=@($pe.ReadDebugDirectory()|Where-Object Type -eq CodeView|ForEach-Object {$cv=$pe.ReadCodeViewDebugDirectoryData($_);@{guid=$cv.Guid.ToString();age=$cv.Age;stamp=$_.Stamp}})
            $informationalVersionPrefix=$null;$informationalCommit=$null
            foreach($handle in $assembly.GetCustomAttributes()){
                $attribute=$reader.GetCustomAttribute($handle)
                if($attribute.Constructor.Kind -ne [Reflection.Metadata.HandleKind]::MemberReference){continue}
                $constructor=$reader.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]$attribute.Constructor)
                if($constructor.Parent.Kind -ne [Reflection.Metadata.HandleKind]::TypeReference){continue}
                $type=$reader.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]$constructor.Parent)
                if($reader.GetString($type.Namespace) -cne 'System.Reflection' -or $reader.GetString($type.Name) -cne 'AssemblyInformationalVersionAttribute'){continue}
                $blob=$reader.GetBlobReader($attribute.Value)
                if($blob.ReadUInt16() -ne 1){throw 'Invalid producer informational version attribute.'}
                $versionText=$blob.ReadSerializedString()
                if($versionText -cmatch '^([0-9]+\.[0-9]+\.[0-9]+)(?:[-+][A-Za-z0-9.+-]+)?$'){$informationalVersionPrefix=$Matches[1]}
                if($versionText -cmatch '\+([a-f0-9]{40})$'){$informationalCommit=$Matches[1]}
            }
            return @{name=$reader.GetString($assembly.Name);version=$assembly.Version.ToString();culture='neutral';publicKeyToken=$token;mvid=$reader.GetGuid($definition.Mvid).ToString();codeView=$debug;informationalVersionPrefix=$informationalVersionPrefix;informationalCommit=$informationalCommit}
        } finally {$pe.Dispose()}
    } finally {$stream.Dispose()}
}
function Stop-DocumentProducerObserver([string]$Root) {
    if(-not [OperatingSystem]::IsLinux()){return}
    Read-DocumentProducerOwner $Root
    $markerPath=Join-Path $Root 'observer.json'
    if(-not [IO.File]::Exists($markerPath)){return}
    $marker=[Text.Encoding]::UTF8.GetString((Read-DocumentProducerBytes $markerPath $Root 4096))|ConvertFrom-Json
    if($marker.run -cne $env:GITHUB_RUN_ID -or $marker.attempt -cne $env:GITHUB_RUN_ATTEMPT){throw 'Observer lifecycle ownership mismatch.'}
    $stop=Join-Path $Root 'observer.stop'
    if(-not [IO.File]::Exists($stop)){Write-DocumentProducerBytes $stop $Root ([byte[]]@(1))}
    $deadline=[DateTime]::UtcNow.AddSeconds(5)
    while([IO.Directory]::Exists("/proc/$($marker.processId)")){
        try {$currentEpoch=Read-DocumentProducerEpoch $marker.processId} catch {if(-not [IO.Directory]::Exists("/proc/$($marker.processId)")){return};throw 'Observer process identity unavailable.'}
        if($currentEpoch -cne $marker.startTicks){throw 'Observer PID was reused; no process will be killed.'}
        if([DateTime]::UtcNow -ge $deadline){
            Assert-DocumentObserverProcess $marker.processId $marker.startTicks $Root
            $process=[Diagnostics.Process]::GetProcessById([int]$marker.processId)
            try {Assert-DocumentObserverProcess $marker.processId $marker.startTicks $Root;$process.Kill();if(-not $process.WaitForExit(1000)){throw 'Observer did not stop.'}}finally{$process.Dispose()};break
        }
        Start-Sleep -Milliseconds 50
    }
}
if($Mode -eq 'Library'){return}
$ErrorActionPreference='Stop'
if(-not [OperatingSystem]::IsLinux()){throw 'Live producer observation requires hosted Linux.'}
$status='incomplete';$stage='startup';$refusalCategory='none';$snapshots=@();$processIdentity=$null;$outputRoot=$null;$total=0L
try {
    Read-DocumentProducerOwner $PrivateDirectory
    if($env:GITHUB_EVENT_NAME -cne 'pull_request' -or $env:GITHUB_REPOSITORY -cne 'MALIEV-Co-Ltd/Legacy.Maliev.DocumentService'){throw 'Producer requires owned hosted PR.'}
    # Startup handshake: an unregistered child exits within two seconds and never observes.
    $registration=Join-Path $PrivateDirectory 'observer.json';$startupDeadline=[DateTime]::UtcNow.AddSeconds(2)
    while(-not [IO.File]::Exists($registration) -and [DateTime]::UtcNow -lt $startupDeadline){Read-DocumentProducerOwner $PrivateDirectory;Start-Sleep -Milliseconds 25}
    $registered=[Text.Encoding]::UTF8.GetString((Read-DocumentProducerBytes $registration $PrivateDirectory 4096))|ConvertFrom-Json
    if($registered.processId -cne $PID.ToString() -or $registered.startTicks -cne (Read-DocumentProducerEpoch $PID.ToString()) -or $registered.run -cne $env:GITHUB_RUN_ID -or $registered.attempt -cne $env:GITHUB_RUN_ATTEMPT){throw 'Observer startup registration invalid.'}
    $outputRoot=Assert-DocumentProducerPath (Join-Path $PrivateDirectory 'producer') $PrivateDirectory
    if([IO.Directory]::Exists($outputRoot)){throw 'Producer capture already exists.'}
    [void][IO.Directory]::CreateDirectory($outputRoot)
    $deadline=[DateTime]::UtcNow.AddMinutes(5);$seen=@{}
    while([DateTime]::UtcNow -lt $deadline -and -not [IO.File]::Exists((Join-Path $PrivateDirectory 'observer.stop'))){
        $stage='diagnostic-envelope'
        Read-DocumentProducerOwner $PrivateDirectory
        $diagnostics=@(Get-ChildItem -LiteralPath $PrivateDirectory -File -Force|Where-Object Name -Like '*.log')
        if($diagnostics.Count -gt 16){throw 'Producer diagnostic file count exceeded.'}
        $diagnosticTotal=0L
        foreach($diagnostic in $diagnostics){
            $bytes=Read-DocumentProducerBytes $diagnostic.FullName $PrivateDirectory 8MB;$diagnosticTotal+=$bytes.Length
            $total+=$bytes.Length
            if($total -gt 256MB){throw 'Producer total read budget exceeded.'}
            if($diagnosticTotal -gt 32MB){throw 'Producer diagnostic aggregate exceeded.'}
            $lines=[Text.Encoding]::UTF8.GetString($bytes).Split([char]10)
            if($lines.Count -gt 131072){throw 'Producer diagnostic line budget exceeded.'}
            foreach($line in $lines){
                if($line.Length -gt 32768){throw 'Producer diagnostic line bound exceeded.'}
                if($line -cmatch '^TpTrace (?:Information|Verbose): 0 : ([1-9][0-9]*), [0-9]+, [0-9]{4}/[0-9]{2}/[0-9]{2}, [0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}, [0-9]+, datacollector\.dll, '){$seen[$Matches[1]]=$true}
            }
        }
        if($seen.Count -gt 1){throw 'Multiple collector processes; no unique producer binding.'}
        if($seen.Count -eq 1){
            $collectorProcessId=[string]@($seen.Keys)[0]
            if(-not [IO.Directory]::Exists("/proc/$collectorProcessId")){break}
            $stage='process-epoch'
            $epoch=Read-DocumentProducerEpoch $collectorProcessId
            $stage='process-user'
            $userId=Get-DocumentProducerUid $collectorProcessId
            if($userId -cne (Get-DocumentProducerUid $PID.ToString())){throw 'Collector process user mismatch.'}
            $launcher=[IO.FileInfo]::new("/proc/$collectorProcessId/exe").LinkTarget
            $stage='executable-launcher'
            if($launcher -cne ([IO.Path]::GetFullPath($env:DOTNET_ROOT).TrimEnd('/')+'/dotnet')){throw 'Producer executable launcher mismatch.'}
            $command=Read-DocumentProducerKernelBytes "/proc/$collectorProcessId/cmdline" 16384
            $stage='managed-launcher'
            $scope=ConvertFrom-DocumentProducerCommand $command $env:DOTNET_ROOT
            [void](Assert-DocumentProducerPath $scope.entryPoint $env:DOTNET_ROOT)
            $stage='mapped-modules'
            $maps=[Text.Encoding]::UTF8.GetString((Read-DocumentProducerKernelBytes "/proc/$collectorProcessId/maps" 4MB))
            $mapped=@{}
            foreach($line in $maps.Split([char]10)){$map=ConvertFrom-DocumentProducerMap $line $scope.sdkDirectory;if($null -ne $map){Add-DocumentProducerMap $mapped $map}}
            if($mapped.Count -eq $documentProducerNames.Count){
                foreach($module in $documentProducerNames){
                    $stage='mapped-file-identity'
                    $map=$mapped[$module];[void](Assert-DocumentProducerPath $map.path $env:DOTNET_ROOT)
                    $before=Get-DocumentProducerInode $map.path
                    if($before.inode -cne $map.inode -or $before.major -ne $map.deviceMajor -or $before.minor -ne $map.deviceMinor){throw 'Mapped file inode/device mismatch.'}
                    foreach($extension in @('dll','pdb')){
                        $stage='snapshot-bytes'
                        $source=[IO.Path]::ChangeExtension($map.path,$extension)
                        [void](Assert-DocumentProducerPath $source $env:DOTNET_ROOT)
                        if($extension -eq 'pdb' -and -not [IO.File]::Exists($source)){continue}
                        $bytes=Read-DocumentProducerBytes $source $env:DOTNET_ROOT;$total+=3*$bytes.Length
                        if($total -gt 256MB){throw 'Producer aggregate read budget exceeded.'}
                        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes));$name=[IO.Path]::GetFileName($source)
                        $copy=Join-Path $outputRoot $name;Write-DocumentProducerBytes $copy $outputRoot $bytes
                        $copied=Read-DocumentProducerBytes $copy $outputRoot;$afterBytes=Read-DocumentProducerBytes $source $env:DOTNET_ROOT
                        if($hash -cne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($copied)) -or $hash -cne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($afterBytes))){throw 'Producer bytes changed during snapshot.'}
                        $snapshot=@{module=$module;extension=$extension;artifact=$name;sha256=$hash;bytes=$bytes.Length;mapped=$extension -eq 'dll';executionAttributed=$false}
                        if($extension -eq 'dll'){$stage='module-metadata';$snapshot.metadata=Get-DocumentProducerMetadata $bytes $module}
                        $snapshots+=$snapshot
                    }
                    $after=Get-DocumentProducerInode $map.path
                    if($before.inode -cne $after.inode -or $before.bytes -ne $after.bytes -or $before.major -ne $after.major -or $before.minor -ne $after.minor){throw 'Producer file epoch changed.'}
                }
                $stage='process-after-snapshot'
                if((Read-DocumentProducerEpoch $collectorProcessId) -cne $epoch){throw 'Producer PID epoch changed.'}
                if((Get-DocumentProducerUid $collectorProcessId) -cne $userId){throw 'Producer user changed.'}
                $afterCommand=ConvertFrom-DocumentProducerCommand (Read-DocumentProducerKernelBytes "/proc/$collectorProcessId/cmdline" 16384) $env:DOTNET_ROOT
                if($scope.entryPoint -cne $afterCommand.entryPoint){throw 'Producer launcher changed.'}
                if([IO.FileInfo]::new("/proc/$collectorProcessId/exe").LinkTarget -cne $launcher){throw 'Producer executable changed.'}
                $afterMaps=[Text.Encoding]::UTF8.GetString((Read-DocumentProducerKernelBytes "/proc/$collectorProcessId/maps" 4MB))
                $afterMapped=@{}
                foreach($line in $afterMaps.Split([char]10)){$map=ConvertFrom-DocumentProducerMap $line $scope.sdkDirectory;if($null -ne $map){Add-DocumentProducerMap $afterMapped $map}}
                foreach($module in $documentProducerNames){if(-not $afterMapped.ContainsKey($module) -or $afterMapped[$module].inode -cne $mapped[$module].inode -or $afterMapped[$module].deviceMajor -ne $mapped[$module].deviceMajor -or $afterMapped[$module].deviceMinor -ne $mapped[$module].deviceMinor){throw 'Producer mapping changed during capture.'}}
                $processIdentity=@{processId=$collectorProcessId;processRole='datacollector.dll';startTicks=$epoch;sdkVersion=$scope.sdkVersion;sdkScope='DOTNET_ROOT/sdk/version/Extensions';sameProcessEpochVerified=$true;sameUserIdVerified=$true;launcherExecutablePathVerified=$true;mappedInodeDeviceBindingVerified=$true;executionAttributed=$false}
                $status='mapped-snapshot';$stage='complete';break
            }
        }
        Start-Sleep -Milliseconds 100
    }
} catch {$status='capture-refused';$refusalCategory=Get-DocumentProducerRefusalCategory $_.Exception.Message;$snapshots=@();$processIdentity=$null}
finally {
    if($null -ne $outputRoot){
        try {
            Read-DocumentProducerOwner $PrivateDirectory
            $receipt=@{schemaVersion=1;status=$status;observationStage=$stage;refusalCategory=$refusalCategory;run=$env:GITHUB_RUN_ID;attempt=$env:GITHUB_RUN_ATTEMPT;process=$processIdentity;snapshots=$snapshots;readBytes=$total;producerSourceVerified=$false;producerExecutionVerified=$false;effectiveSettingsVerified=$false;transformationVerified=$false;evidenceComplete=$false;policyActive=$false;runtimeAccepted=$false;rawNumericalPassed=$false}
            Write-DocumentProducerBytes (Join-Path $outputRoot 'receipt.json') $outputRoot ([Text.Encoding]::UTF8.GetBytes(($receipt|ConvertTo-Json -Depth 12)))
        } catch { }
    }
}
