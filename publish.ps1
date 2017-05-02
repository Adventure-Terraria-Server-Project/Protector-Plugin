#
# Terraria Plugin Publishing Script, CoderCow 2017
#
# 
# Actions Performed by this Script
# 
#   * Uses GitVersion to increment a component of the version number in the project's AssemblyInfo.cs.
#     The component of the SemVer version number to increase (major, minor or patch) is determined by 
#     the commits done since the last tag (BREAKING, feature or fix commits).
#   * Uses clog to generate a changelog in markdown format. The entries are also determined by the 
#     commits since the last tag.
#   * Bundles release files in a zip file.
#   * Creates a commit for the changed AssemblyInfo.cs and tags this commit with the new SemVer,
#     also pushes this commit and the tag.
#   * Uses GitReleaseManager to create a GitHub release and attaches the zip file to it, using the
#     generated changelog as the description text.
#   * Performs a login on https://tshock.co and edits the related plugin resource to update its
#     version information.
#   * Converts the repository's README.md to html and uses it as the description text for the tshock
#     resource, so that descriptions remain consistent.
#   * Adds a resource update to tshock.co using using the converted changelog.md as description.
#
# Usage Notes
#
#   The conventional commit message format of angularjs is expected when scanning recent commits. 
#   It's documented here: https://github.com/conventional-changelog/conventional-changelog/blob/a5505865ff3dd710cf757f50530e73ef0ca641da/conventions/angular.md
#
#   Do not run this script when you're currently on a tagged commit. 
#   Do not commit AssemblyInfo.cs before invoking this script.
#
#   To create pre-releases, make a new branch and call it "pre". When running this script while being on
#   a commit in this branch, versions will looks like this "X.X.X-pre.Y" where X.X.X is your next release
#   version and Y your current pre-release candidate. The version should become "X.X.X" (without the suffix) 
#   once you merge "pre" (fast forward should work fine) back into master and run this script again.
#
# Installing Dependencies
#
#   If you have chocolatey, most of the required dependencies can be installed by:
#     choco install GitReleaseManager.Portable GitVersion.Portable 7zip rust -y & cargo install clog-cli
# 
#   Note that cargo is the bundled package manager of rust.
#   You may have to register some of the binaries in your PATH.
#
#   To install the markdown powershell module, see this: 
#   https://social.technet.microsoft.com/wiki/contents/articles/30591.convert-markdown-to-html-using-powershell.aspx
#
# Further Notes
# 
#   In case you prefer GitHub issues for the changelog generation instead of commits you might rewrite 
#   this script to use GitReleaseManager for this, as it supports changelogs based on issues and milestones.
#
#   clog repo: https://github.com/clog-tool/clog-cli
#   GitVersion docs: http://gitversion.readthedocs.io
#   GitReleaseManager docs: http://gitreleasemanager.readthedocs.io

$ErrorActionPreference = "Stop"

Import-Module PowershellMarkdown

$outDir = "$PSScriptRoot\bin\Release"
$assemblyInfoPath = "$PSScriptRoot\Properties\AssemblyInfo.cs"
# the *.cs file which contains the main plugin class annontated with the ApiVersion attribute
$pluginSourceFilePath = "$PSScriptRoot\Implementation\ProtectorPlugin.cs"
# the tshock binary is used to determine the tshock version this plugin was built against
$tshockBinaryPath = "$outDir\TShockAPI.dll"
# the OTAPI binary is used to determine the Terraria version this plugin was built against
$otapiBinaryPath = "$outDir\OTAPI.dll"
$targetName = "Protector"
$projectFile = "$PSScriptRoot\$targetName.csproj"
$commitMessageFormat = "chore(version): tick plugin version {0}"
$tagNameFormat = "release {0} for Terraria {1} (API {2})"
$outZipFileNameFormat = "Protector_{0}_API_{2}.zip"

$gitHubUser = "CoderCow"
$gitHubRepoOwner = "CoderCow"
$gitHubRepoName = "Protector-Plugin"

# information used to update the ressource on tshock.co
$tshockUser = "CoderCow@gmx.de"
$tshockResourceUri = "https://tshock.co/xf/index.php?resources/protector.190/"
# for the XenForo description text
$readmeFile = "$PSScriptRoot\README.md"

$binariesToPublish = @(
  "$outDir\$targetName.dll",
  "$outDir\$targetName.pdb",
  "$outDir\Plugin Common Lib *.dll",
  "$outDir\Plugin Common Lib *.pdb"
)

function Main {
  $pluginApiVersion = Get-ApiVersion
  Write-Host "The plugin's API Version is $pluginApiVersion" -ForegroundColor Cyan

  $tshockVersion = Get-TshockVersion
  Write-Host "This plugin was built against TShock $tshockVersion" -ForegroundColor Cyan

  $terrariaVersion = Get-OtapiVersion
  Write-Host "This plugin was built against Terraria $terrariaVersion" -ForegroundColor Cyan

  $versionInfo = Update-AssemblyVersion
  $releaseVersion = $versionInfo.SemVer
  $isPrerelease = $versionInfo.PreReleaseTag -ne ""
  Write-Host "Release version will be $releaseVersion"

  $outChangelogFile = "$outDir\changelog.md"
  Generate-Changelog $pluginApiVersion $tshockVersion $terrariaVersion $outChangelogFile

  $outZipFile = "$outDir\" + ($outZipFileNameFormat -f $releaseVersion,$terrariaVersion,$pluginApiVersion)
  Package-Files $outZipFile

  Create-Commit $releaseVersion $terrariaVersion $pluginApiVersion

  Write-Host "Publishing to GitHub..."
  Create-GitHubRelease $releaseVersion $outChangelogFile $outZipFile
  Start-Process "https://github.com/$gitHubUser/$gitHubRepoName/releases"

  Write-Host "Updating TShock resource..."
  Update-TShockResource $releaseVersion $terrariaVersion $pluginApiVersion $outChangelogFile
  Start-Process "$tshockResourceUri/updates"
}

function Get-ApiVersion {
  # Try to parse the ApiVersion from the c-sharp file
  $regExResult = Get-Content $pluginSourceFilePath | Select-String "\[\s*ApiVersion\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)"
  if ($regExResult -eq $null) {
    Write-Host "Failed parsing API Version from file $pluginSourceFilePath"
    exit 1
  }
  $apiMajor = $regExResult.Matches[0].Groups[1].Value
  $apiMinor = $regExResult.Matches[0].Groups[2].Value
  "$apiMajor.$apiMinor"
}

function Get-TShockVersion {
  # Get the file version of the TShock binary
  [System.Diagnostics.FileVersionInfo]::GetVersionInfo($tshockBinaryPath).FileVersion.ToString()
}

function Get-OtapiVersion {
  # Get the file version of the OTAPI binary
  [System.Diagnostics.FileVersionInfo]::GetVersionInfo($otapiBinaryPath).FileVersion.ToString()
}

function Update-AssemblyVersion {
  # GitVersion will increment the assembly version and return some information about it in json format
  # Actually, this should have happened already be the pre-build event configured for the project, so this
  # additional call to GitVersion will just return the current version number.
  GitVersion.exe /updateassemblyinfo $assemblyInfoPath | ConvertFrom-Json
}

function Generate-Changelog($pluginApiVersion, $tshockVersion, $terrariaVersion, $outChangelogFile) {
  if (Test-Path $outChangelogFile) {
    Remove-Item -Force $outChangelogFile
  }

  # clog builds a markdown changelog from all commits since the last tag
  clog.exe --from-latest-tag --setversion $releaseVersion --outfile $outChangelogFile

  # add some custom lines to the changelog
  if ($isPrerelease) {
    Add-Content "$outChangelogFile" "**NOTE: This is a pre-release currently under test.**`n"
  }
  Add-Content "$outChangelogFile" "Plugin API Version: **$pluginApiVersion**. It was built against Terraria **$terrariaVersion** and TShock **$tshockVersion**."
  
  # print the changelog for validation
  Write-Host "---- Content of $outChangelogFile ----" -ForegroundColor Green
  Get-Content $outChangelogFile | Write-Host -ForegroundColor Cyan
  Write-Host "------------- EOF -------------" -ForegroundColor Green

  $wantToEdit = Read-Host "Do you want to edit the changelog? [y/n]"
  if ($wantToEdit -eq "y") {
    Start-Process $outChangelogFile
    Read-Host "Press any key to continue"
  }
}

function Package-Files($outZipFile) {
  $outPluginDir = "$outDir\ServerPlugins"
  $outTShockDir = "$outDir\tshock"

  if (Test-Path $outZipFile) {
    Remove-Item -Force $outZipFile
  }

  New-Item -ItemType directory -Force $outPluginDir > $null
  Move-Item $binariesToPublish -Force "$outPluginDir\"
  7z.exe a -y -r -bd -tzip -mx9 $outZipFile $outPluginDir $outTShockDir > $null
}

function Create-Commit($releaseVersion, $terrariaVersion, $pluginApiVersion) {
  $tagName = $tagNameFormat -f $releaseVersion,$terrariaVersion,$pluginApiVersion
  $commitMessage = $commitMessageFormat -f $releaseVersion,$terrariaVersion,$pluginApiVersion

  git add $assemblyInfoPath
  git commit --message $commitMessage
  git tag --annotate $releaseVersion --message $tagName
}

function Create-GitHubRelease($releaseVersion, $outChangelogFile, $outZipFile) {
  $gitHubPassword = Read-Host "Enter password for GitHub user $gitHubUser"

  # This ensures that errors can be seen if they happen
  $ErrorActionPreference = "Continue"

  git push origin --follow-tags
  GitReleaseManager.exe create -u $gitHubUser -p $gitHubPassword -o $gitHubRepoOwner -r $gitHubRepoName -n $releaseVersion -i $outChangelogFile -a $outZipFile
  GitReleaseManager.exe publish -u $gitHubUser -p $gitHubPassword -o $gitHubRepoOwner -r $gitHubRepoName -t $releaseVersion
}

function Update-TShockResource($releaseVersion, $terrariaVersion, $pluginApiVersion, $changelogFile) {
  $tshockPassword = Read-Host "Enter password for TShock XenForo user $tshockUser"

  # Invoke-WebRequest: https://msdn.microsoft.com/powershell/reference/5.1/microsoft.powershell.utility/Invoke-WebRequest
  # FormObject: https://msdn.microsoft.com/en-us/library/microsoft.powershell.commands.formobject(v=vs.85).aspx

  # authenticate
  $response = Invoke-WebRequest -Uri "https://tshock.co/xf/index.php?login" -SessionVariable session
  $formObject = $response.Forms["pageLogin"]
  $formObject.Fields["login"] = $tshockUser
  $formObject.Fields["password"] = $tshockPassword
  $response = Invoke-WebRequest -Uri "https://tshock.co/xf/index.php?login/login" -Method Post -Body $formObject -WebSession $session

  # if this causes a 403 then the login has probably failed
  $response = Invoke-WebRequest -Uri "$tshockResourceUri/edit" -WebSession $session

  # form in that page has no id, so finding it is a bit of a hassle
  $formHtmlElement = $response.ParsedHtml.IHTMLDocument3_getElementsByTagName("form") | Where { $_.action.EndsWith("/save") }
  $fields = Construct-FormFields $response $formHtmlElement

  # can't set the version prefix directly, need a prefix id instead
  # <optgroup label="Version">
  $prefixOptionsList = $response.ParsedHtml.IHTMLDocument3_getElementsByTagName("optgroup") | Where { $_.label -eq "Version" }
  # <option value="6" data-css="prefix prefixRed" >1.18 (obsolete)</option>
  $prefixOption = $prefixOptionsList.getElementsByTagName("option") | Where { $_.innerText -eq $pluginApiVersion }
  if ($prefixOption) {
    $prefixId = $prefixOption.value
    $fields["prefix_id"] = $prefixId
  } else {
    Write-Host "Looks like there's yet no prefix for API $pluginApiVersion available." -ForegroundColor Cyan
    Write-Host "Keeping the current prefix for now." -ForegroundColor Green
  }

  # this is actually "API Version"
  $fields["custom_fields[tshockver]"] = $pluginApiVersion
  $fields["custom_fields[tshockversion]"] = $tshockVersion
  $fields["version_string"] = $releaseVersion

  $readmeMarkdown = Get-Content $readmeFile
  $readmeMarkdown = ($readmeMarkdown -join "`n")
  $readmeMarkdown = $readmeMarkdown -replace "<","&lt;"
  $readmeMarkdown = $readmeMarkdown -replace ">","&gt;"
  $readmeHtml = ConvertFrom-Markdown -MarkdownContent ($readmeMarkdown -join "`n")
  # make things a bit prettier because XenForo does some weird transformations to the html
  $readmeHtml = $readmeHtml -replace "<h3>","<br><h3>"
  $readmeHtml = $readmeHtml -replace "</p>[\s\n\r]*<p>","<br><br>"
  $fields["message_html"] = $readmeHtml

  # save the resource
  $response = Invoke-WebRequest -Uri "$tshockResourceUri/save" -Method Post -Body $fields -WebSession $session

  # open "Post Resource Update" page
  $response = Invoke-WebRequest -Uri "$tshockResourceUri/add-version" -WebSession $session
  $formHtmlElement = $response.ParsedHtml.IHTMLDocument3_getElementsByTagName("form") | Where { $_.action.EndsWith("/save-version") }
  
  $fields = Construct-FormFields $response $formHtmlElement
  $fields["download-url"] = "https://github.com/$gitHubUser/$gitHubRepoName/releases/tag/$releaseVersion"
  $fields["version-string"] = $releaseVersion
  $fields["title"] = "$releaseVersion Update"
  $fields["message_html"] = "$releaseVersion Update"

  $changelogMarkdown = Get-Content $changelogFile
  # remove commit hashes
  $changelogMarkdown = $changelogMarkdown -replace "\(\[[A-Za-z0-9]+\]\([A-Za-z0-9]+\)\)",""

  $changelogHtml = ConvertFrom-Markdown -MarkdownContent ($changelogMarkdown -join "`n")
  # make things a bit prettier because XenForo does some weird transformations to the html
  $changelogHtml = $changelogHtml -replace "<h4>","<br><h4>"
  $fields["message_html"] = $changelogHtml

  # post update
  $response = Invoke-WebRequest -Uri "$tshockResourceUri/save-version" -Method Post -Body $fields -WebSession $session
}

function Construct-FormFields($request, $formHtmlElement) {
  $fields = @{}
  $inputFields = $formHtmlElement.getElementsByTagName("input") | Where { $_.name -and $_.type -ne "button" }
  foreach ($newField in $inputFields) {
    $fields[$newField.name] = $newField.Value 
  }

  foreach ($newField in $formHtmlElement.getElementsByTagName("textarea")) {
    $fields.Add($newField.name, $newField.Value)
  }

  foreach ($newField in $formHtmlElement.getElementsByTagName("select")) {
    $fields.Add($newField.name, $newField.Value)
  }

  return $fields
}

Main