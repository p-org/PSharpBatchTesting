$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
function RunPSharpBatchTester(){
    $ProjectLocation = Get-Project | Select-Object -ExpandProperty FullName
    $ProjectFolderLocation = Split-Path -parent $ProjectLocation

    # Config Files
    $configFilePath = Join-Path $ProjectFolderLocation "PSharpBatch.config"
    $AuthConfigPath = Join-Path $ProjectFolderLocation "PSharpBatchAuth.config"

    #Converting to Arguments
    $configFilePath = "/config:$configFilePath"
    $AuthConfigPath = "/auth:$AuthConfigPath"

    #Getting Arguments
    $arguments = $Args -join " "

    #Getting the location of the test dll/exe
    $ProjectName = Split-Path -Leaf $ProjectFolderLocation
    $TestApplicationDll = $ProjectName + '.dll'
    $TestApplicationExe = $ProjectName + '.exe'
    $TestApplicationDebugPath = Join-Path $ProjectFolderLocation 'bin'
    $TestApplicationDebugPath = Join-Path $TestApplicationDebugPath 'Debug'
    $TestApplicationDllPath = Join-Path $TestApplicationDebugPath $TestApplicationDll
    $TestApplicationExePath = Join-Path $TestApplicationDebugPath $TestApplicationExe

    #PSharp Binaries Path
    # $PSharpBinariesLocation = Get-Item Env:PSharpBinaries | Select-Object -ExpandProperty Value
    # $PSharpBinariesFlag = "/binaries:$PSharpBinariesLocation"
    # if(!$PSharpBinariesLocation -or !(Test-Path $PSharpBinariesLocation) ){
    #     'PSharp Binaries folder error'
    #     exit
    # }

    #Getting the BatchTester Location
    $toolsPath = $scriptPath
    if($PSScriptRoot){ $toolsPath = $PSScriptRoot }

    # Run the BatchTester application
    $BatchTesterFolderPath = Join-Path $toolsPath "BatchTester"
    $BatchTesterPath = Join-Path $BatchTesterFolderPath "PsharpBatchTester.exe"

    #Running the batch test.
    & $BatchTesterPath $configFilePath $AuthConfigPath $arguments
}

Export-ModuleMember RunPSharpBatchTester