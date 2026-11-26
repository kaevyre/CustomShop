# Redacts plaintext secrets from Platform.AppHost/aspirate-output before commit.
# aspirate puts some secrets (Jwt__SecretKey, *PASSWORD*) into ConfigMap literals of kustomization.yaml.
# Run it AFTER deployment and screenshots, BEFORE `git add`. To deploy again: `aspirate generate` re-creates the output.
#   ./scripts/redact-aspirate-secrets.ps1
$root = Join-Path $PSScriptRoot '..\Platform.AppHost\aspirate-output'
$enc = New-Object Text.UTF8Encoding $false
Get-ChildItem $root -Recurse -Filter kustomization.yaml | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    $new = [regex]::Replace($text, '(?m)^(\s*- (?:Jwt__SecretKey|[A-Z0-9_]*PASSWORD[A-Z0-9_]*))=.*$', '$1=<redacted>')
    if ($new -ne $text) {
        [IO.File]::WriteAllText($_.FullName, $new, $enc)
        Write-Host "redacted: $($_.FullName)"
    }
}
