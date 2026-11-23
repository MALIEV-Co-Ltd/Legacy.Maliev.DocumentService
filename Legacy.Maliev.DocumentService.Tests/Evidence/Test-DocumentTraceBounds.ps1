# Synthetic typed controls only: no file reads, processes, module loading or raw payload output.
function Test-DocumentTraceBounds {
    $passed=0
    $read={param([string[]]$Lines) Read-DocumentCollectorTrace -Lines $Lines -ExpectedTestModule '/synthetic/tests.dll' -ExpectedApplicationModule '/synthetic/app.dll' -ModuleBindings @{}}
    $check={param([bool]$Condition) if(-not $Condition){throw 'Trace bound control failed.'}}
    $reject={param([scriptblock]$Operation,[string]$Message) $rejected=$false;try{& $Operation|Out-Null}catch{if($_.Exception.Message.StartsWith($Message,[StringComparison]::Ordinal)){$rejected=$true}else{throw 'Trace bound control unexpected refusal.'}};if(-not $rejected){throw 'Trace bound control accepted overbound input.'}}
    $envelope='TpTrace Information: 0 : 42, 1, 2026/10/06, 18:00:00.000, 1, datacollector.dll, '
    $session=$envelope+'[coverlet]CoverletCoverageDataCollector: SessionStart received'
    foreach($length in @(33334,65536)){
        $result=& $read -Lines @([string]::new('x',$length),$session)
        & $check ($result.events.Count -eq 1 -and $result.events[0].kind -ceq 'collector-session-start' -and -not $result.evidenceComplete -and -not $result.runtimeAccepted -and -not $result.policyActive -and -not $result.rawDiagnosticsExported)
        $passed++
    }
    & $reject {& $read -Lines @([string]::new('x',65537))} 'Trace line length budget exceeded';$passed++
    & $reject {& $read -Lines @($session+[string]::new('x',32769-$session.Length))} 'Trace line length budget exceeded';$passed++
    & $reject {& $read -Lines ([string[]]::new(131073))} 'Trace line budget exceeded';$passed++
    $unknown=& $read -Lines @($envelope+'[coverlet]CoverletCoverageDataCollector: unsupported')
    & $check ($unknown.unrecognizedRelevantLineCount -eq 1 -and $unknown.events.Count -eq 0 -and -not $unknown.evidenceComplete);$passed++
    $bytes=[Text.Encoding]::UTF8.GetBytes([string]::new('x',33334)+"`n"+$session+"`n")
    $ids=@(Get-DocumentProducerDiagnosticProcessIds $bytes)
    & $check ($ids.Count -eq 1 -and $ids[0] -ceq '42');$passed++
    & $reject {ConvertFrom-DocumentProducerDiagnosticLines ([Text.Encoding]::UTF8.GetBytes([string]::new('x',65537)+"`n"))} 'Producer diagnostic line bound exceeded';$passed++
    & $reject {ConvertFrom-DocumentProducerDiagnosticLines ([Text.Encoding]::UTF8.GetBytes($session+[string]::new('x',32769-$session.Length)+"`n"))} 'Producer diagnostic line bound exceeded';$passed++
    & $reject {ConvertFrom-DocumentProducerDiagnosticLines ([byte[]]::new(8MB+1))} 'Producer diagnostic input exceeds byte bound';$passed++
    $command={param([string[]]$Arguments) [Text.Encoding]::UTF8.GetBytes(($Arguments -join [char]0)+[char]0)}
    $sdk='/synthetic/dotnet/sdk/11.0.100'
    $valid=@('/synthetic/dotnet/dotnet','exec','--runtimeconfig',"$sdk/datacollector.runtimeconfig.json",'--depsfile',"$sdk/datacollector.deps.json","$sdk/datacollector.dll",'--port','1234')
    $scope=ConvertFrom-DocumentProducerCommand (& $command $valid) '/synthetic/dotnet'
    & $check ($scope.sdkVersion -ceq '11.0.100' -and $scope.entryPoint -ceq "$sdk/datacollector.dll" -and $scope.launcherOptions.runtimeConfig -and $scope.launcherOptions.depsFile);$passed++
    $old=@('/synthetic/dotnet/dotnet','exec','/synthetic/dotnet/sdk/10.0.401/datacollector.dll')
    & $reject {ConvertFrom-DocumentProducerCommand (& $command $old) '/synthetic/dotnet'} 'Unexpected producer SDK entry point';$passed++
    $reversed=@('/synthetic/dotnet/dotnet','exec','--depsfile',"$sdk/datacollector.deps.json",'--runtimeconfig',"$sdk/datacollector.runtimeconfig.json","$sdk/datacollector.dll")
    & $reject {ConvertFrom-DocumentProducerCommand (& $command $reversed) '/synthetic/dotnet'} 'Unexpected producer launcher options';$passed++
    $outside=@('/synthetic/dotnet/dotnet','exec','--runtimeconfig','/tmp/datacollector.runtimeconfig.json',"$sdk/datacollector.dll")
    & $reject {ConvertFrom-DocumentProducerCommand (& $command $outside) '/synthetic/dotnet'} 'Unexpected producer launcher options';$passed++
    if($passed -ne 14){throw 'Trace bound control count mismatch.'}
    Write-Host '[document-trace-bounds] 14 synthetic controls passed; no runtime acceptance inferred.'
}
