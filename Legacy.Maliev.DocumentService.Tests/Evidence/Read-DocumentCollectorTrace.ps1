# Narrow, fail-closed typed extraction. Never exports diagnostic messages, configuration XML,
# absolute paths, environment values or arbitrary strings. A successful load trace is distinct
# from a pre-load attempt. Actual producer-byte/source and transformation joins remain required.
function Read-DocumentCollectorTrace {
    param([string[]]$Lines,[string]$ExpectedTestModule,[string]$ExpectedApplicationModule,[hashtable]$ModuleBindings,[bool]$CallerVerifiedModuleByteBindings=$false)
    $events=@();$unrecognizedRelevant=0;$outsideCollector=0
    $structure=[ordered]@{
        envelopeMatchedRelevant=0;envelopeUnmatchedRelevant=0
        knownRoleCounts=[ordered]@{datacollector=0;testhost=0;vstestConsole=0;dotnet=0;other=0}
        messageFamilyCounts=[ordered]@{successfulResolution=0;initialize=0;sessionStart=0;sessionEnd=0;parsedSettings=0;instrumentedModule=0;otherCollector=0;otherRelevant=0}
        wrapperCounts=[ordered]@{exactCoverlet=0;missingOrUnknown=0}
        bindingMismatchCounts=[ordered]@{pathNotBound=0;identityMismatch=0;grammarMismatch=0}
    }
    foreach($key in $ModuleBindings.Keys){
        $binding=$ModuleBindings[$key]
        if(-not [IO.Path]::IsPathRooted($key) -or $binding.module -cnotin @('coverlet.collector.dll','coverlet.core.dll','Mono.Cecil.dll') -or $binding.sha256 -cnotmatch '^[A-F0-9]{64}$' -or $binding.identity -cnotmatch '^(coverlet\.(collector|core)|Mono\.Cecil), Version=[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+, Culture=(neutral|[A-Za-z-]+), PublicKeyToken=(null|[0-9a-f]{16})$'){throw 'Invalid module binding contract.'}
        $simpleName=$binding.identity.Substring(0,$binding.identity.IndexOf(',', [StringComparison]::Ordinal))
        if([IO.Path]::GetFileName($key) -cne $binding.module -or ($simpleName+'.dll') -cne $binding.module){throw 'Module label/path/identity mismatch.'}
    }
    if($Lines.Count -gt 131072){throw 'Trace line budget exceeded.'}
    foreach($line in $Lines){
        if($line.Length -gt 32768){throw 'Trace line length budget exceeded.'}
        if($line -notmatch 'CoverletCoverageDataCollector|Instrumented module:|AssemblyResolver\.OnResolve: Resolved assembly: (coverlet\.(collector|core)|Mono\.Cecil),'){continue}
        if($line -notmatch '^TpTrace (Information|Verbose): 0 : ([0-9]+), ([0-9]+), ([0-9]{4}/[0-9]{2}/[0-9]{2}), ([0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}), ([0-9]+), ([^,]+), (.*)$'){
            $unrecognizedRelevant++;$structure.envelopeUnmatchedRelevant++
            continue
        }
        $structure.envelopeMatchedRelevant++
        $trace=@{}+$Matches
        $message=$trace[8]
        $wrapper=$message.StartsWith('[coverlet]',[StringComparison]::Ordinal)
        if($wrapper){$message=$message.Substring(10);$structure.wrapperCounts.exactCoverlet++}else{$structure.wrapperCounts.missingOrUnknown++}
        $role=switch -CaseSensitive ($trace[7]){'datacollector.dll'{'datacollector'};'testhost.dll'{'testhost'};'vstest.console.dll'{'vstestConsole'};'dotnet'{'dotnet'};default{'other'}}
        $structure.knownRoleCounts[$role]++
        $family=if($message.StartsWith('AssemblyResolver.OnResolve: Resolved assembly: ',[StringComparison]::Ordinal)){'successfulResolution'}
            elseif($message.StartsWith('Initializing CoverletCoverageDataCollector with configuration: ',[StringComparison]::Ordinal)){'initialize'}
            elseif($message -ceq 'CoverletCoverageDataCollector: SessionStart received'){'sessionStart'}
            elseif($message -ceq 'CoverletCoverageDataCollector: SessionEnd received'){'sessionEnd'}
            elseif($message.StartsWith('CoverletCoverageDataCollector: Initializing coverlet process with settings: ',[StringComparison]::Ordinal)){'parsedSettings'}
            elseif($message.StartsWith('Instrumented module: ',[StringComparison]::Ordinal)){'instrumentedModule'}
            elseif($message.StartsWith('CoverletCoverageDataCollector:',[StringComparison]::Ordinal)){'otherCollector'}else{'otherRelevant'}
        $structure.messageFamilyCounts[$family]++
        if($trace[7] -cne 'datacollector.dll'){$outsideCollector++;continue}
        if($family -eq 'successfulResolution' -and $wrapper){$structure.bindingMismatchCounts.grammarMismatch++;$unrecognizedRelevant++;continue}
        # Actual pinned TestPlatformEqtTrace prepends this exact wrapper. Do not accept a
        # generic stripped prefix or naked collector messages as the same protocol.
        if($family -ne 'successfulResolution' -and -not $wrapper){$unrecognizedRelevant++;continue}
        $entry=[ordered]@{kind=$null;processId=$trace[2];threadId=$trace[3];processRole='datacollector.dll';timestampLocal=($trace[4]+' '+$trace[5]);timestampOffsetKnown=$false;monotonicCounter=$trace[6];executionAttributed=$false}
        if($message -match '^AssemblyResolver\.OnResolve: Resolved assembly: (coverlet\.(?:collector|core)|Mono\.Cecil), Version=([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+), Culture=(neutral|[A-Za-z-]+), PublicKeyToken=(null|[0-9a-f]{16}), from path: (.+)$'){
            $identity=$Matches[1]+', Version='+$Matches[2]+', Culture='+$Matches[3]+', PublicKeyToken='+$Matches[4]
            $path=$Matches[5]
            if(@($ModuleBindings.Keys|Where-Object {$_ -ceq $path}).Count -eq 1 -and $ModuleBindings[$path].identity -ceq $identity){
                $entry.kind='successful-resolution';$entry.module=$ModuleBindings[$path].module;$entry.sha256=$ModuleBindings[$path].sha256;$entry.reportedIdentityMatchesBinding=$true;$entry.callerVerifiedModuleByteBindings=$CallerVerifiedModuleByteBindings
            }else{
                if(@($ModuleBindings.Keys|Where-Object {$_ -ceq $path}).Count -ne 1){$structure.bindingMismatchCounts.pathNotBound++}else{$structure.bindingMismatchCounts.identityMismatch++}
                $unrecognizedRelevant++;continue
            }
        }elseif($message -match "^Initializing CoverletCoverageDataCollector with configuration: '(.*)'$"){
            $xml=$Matches[1];$known=$false;$format=$null
            if($xml.Length -le 16384){
                $options=[Xml.XmlReaderSettings]::new();$options.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$options.XmlResolver=$null;$options.MaxCharactersInDocument=16384
                try{
                    if($xml -ceq ''){$known=$true;$format='cobertura'}
                    else{
                        $text=[IO.StringReader]::new($xml);$reader=[Xml.XmlReader]::Create($text,$options)
                        try{$doc=[Xml.XmlDocument]::new();$doc.XmlResolver=$null;$doc.Load($reader)}finally{$reader.Dispose();$text.Dispose()}
                        $known=$doc.DocumentElement.Name -ceq 'Configuration' -and $doc.DocumentElement.Attributes.Count -eq 0
                        $seen=@{}
                        foreach($child in $doc.DocumentElement.ChildNodes){
                            if($child.NodeType -eq [Xml.XmlNodeType]::Whitespace){continue}
                            if($child.NodeType -ne [Xml.XmlNodeType]::Element -or $child.Attributes.Count -ne 0 -or $seen.ContainsKey($child.Name)){$known=$false;continue}
                            $seen[$child.Name]=$true
                            switch -CaseSensitive ($child.Name){
                                'Format' {if($child.InnerText.Trim() -ceq 'cobertura'){$format='cobertura'}else{$known=$false}}
                                {$_ -cin @('UseSourceLink','SingleHit','IncludeTestAssembly','SkipAutoProps','DeterministicReport')} {if($child.InnerText.Trim() -cnotin @('false','False')){$known=$false}}
                                {$_ -cin @('Include','IncludeDirectory','ExcludeByFile','ExcludeByAttribute','MergeWith','DoesNotReturnAttribute','ExcludeAssembliesWithoutSources')} {if($child.InnerText -cne ''){$known=$false}}
                                'Exclude' {if($child.InnerText -cne ''){$known=$false}}
                                default {$known=$false}
                            }
                            if(@($child.ChildNodes|Where-Object NodeType -EQ ([Xml.XmlNodeType]::Element)).Count -gt 0){$known=$false}
                        }
                        if($null -eq $format){$format='cobertura'}
                    }
                }catch{$known=$false}
            }
            $entry.kind='collector-initialize';$entry.configurationKnownProfile=$known;$entry.reportFormat=if($known){$format}else{$null};$entry.configurationXmlExported=$false
        }elseif($message -ceq 'CoverletCoverageDataCollector: SessionStart received'){$entry.kind='collector-session-start'}
        elseif($message -ceq 'CoverletCoverageDataCollector: SessionEnd received'){$entry.kind='collector-session-end'}
        elseif($message.StartsWith('CoverletCoverageDataCollector: Initializing coverlet process with settings: ',[StringComparison]::Ordinal)){
            $expected='CoverletCoverageDataCollector: Initializing coverlet process with settings: "TestModule: '''+$ExpectedTestModule+''', IncludeFilters: '''', IncludeDirectories: '''', ExcludeFilters: ''[coverlet.*]*'', ExcludeSourceFiles: '''', ExcludeAttributes: '''', MergeWith: '''', UseSourceLink: ''False''SingleHit: ''False''IncludeTestAssembly: ''False''SkipAutoProps: ''False''DoesNotReturnAttributes: ''''DeterministicReport: ''False''ExcludeAssembliesWithoutSources: ''''"'
            $known=$message -ceq $expected
            $entry.kind='collector-parsed-settings';$entry.parsedKnownProfile=$known
            if($known){$entry.settings=@{includeFilters=@();includeDirectories=@();excludeFilters=@('[coverlet.*]*');excludeFiles=@();excludeAttributes=@();mergeWith=$null;useSourceLink=$false;singleHit=$false;includeTestAssembly=$false;skipAutoProps=$false;doesNotReturnAttributes=@();deterministicReport=$false;excludeAssembliesWithoutSourcesInput=$null;coreDefaultSemanticsVerified=$false}}
        }elseif($message -ceq ("Instrumented module: '"+$ExpectedApplicationModule+"'")){$entry.kind='application-instrumentation-completed';$entry.module='Legacy.Maliev.DocumentService.Application.dll'}
        else{if($family -eq 'successfulResolution'){$structure.bindingMismatchCounts.grammarMismatch++};$unrecognizedRelevant++;continue}
        $events+=$entry
        if($events.Count -gt 512){throw 'Typed trace event budget exceeded.'}
    }
    # This is observational evidence only. Do not infer execution from a mention/resolution,
    # nor claim effective completeness without the real producer and downstream settings join.
    return [ordered]@{schemaVersion=2;events=$events;structuralClassification=$structure;unrecognizedRelevantLineCount=$unrecognizedRelevant;outsideCollectorProcessCount=$outsideCollector;rawDiagnosticsExported=$false;producerBinarySourceVerified=$false;executedCollectorSelectionVerified=$false;effectiveSettingsVerified=$false;transformationVerified=$false;evidenceComplete=$false;policyActive=$false;runtimeAccepted=$false;rawNumericalPassed=$false}
}
