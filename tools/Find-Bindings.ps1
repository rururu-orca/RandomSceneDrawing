<#
.SYNOPSIS
    Extract Bindings in the Xaml file.
.PARAMETER XamlFile
    Xaml file to be extracted.
.EXAMPLE
    PS C:\> Find-Bindings .\src\RandomSceneDrawing\MainWindow.xaml

    Name        Value
    ----        -----
    Command     {Binding Pause}
    Command     {Binding Play}
    Command     {Binding Randomize}
    Text        {Binding CurrentDuration}
    …

#>
using namespace System.IO
using namespace System.Windows.Markup

[CmdletBinding()]
param (
    [Parameter(Mandatory,ValueFromPipeline)]
    [ValidateScript({Test-Path $_ -Filter *.xaml})]
    [FileInfo]
    $XamlFile
)

[xml]$xaml = Get-Content $XamlFile

$xaml.Window.Attributes
| Where-Object Name -Match ^xmlns
| ForEach-Object {
    (Select-Xml "//_:*" $xaml -Namespace @{'_'=$_.Value}).Node.Attributes
    | Where-Object Value -match '{Binding\s'
    | Select-Object Name,Value
    | Sort-Object Name,Value -Unique
}