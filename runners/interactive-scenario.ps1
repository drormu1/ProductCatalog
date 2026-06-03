param(
    [string]$HostUrl = "http://localhost:5088"
)

$ErrorActionPreference = "Stop"
$MaxConcurrency = 25
$MaxAttempts = 200

function Invoke-Api {
    param(
        [Parameter(Mandatory)] [ValidateSet("GET", "POST", "PUT")] [string]$Method,
        [Parameter(Mandatory)] [string]$Url,
        [string]$Body
    )

    try {
        $requestParams = @{
            Method          = $Method
            Uri             = $Url
            ErrorAction     = "Stop"
            UseBasicParsing = $true
        }

        if (-not [string]::IsNullOrWhiteSpace($Body)) {
            $requestParams.ContentType = "application/json"
            $requestParams.Body = $Body
        }

        $response = Invoke-WebRequest @requestParams
        $content = $null

        if (-not [string]::IsNullOrWhiteSpace($response.Content)) {
            try { $content = $response.Content | ConvertFrom-Json }
            catch { $content = $response.Content }
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = $content
        }
    }
    catch {
        $statusCode = 0
        $content = $null

        if ($_.Exception.Response) {
            try { $statusCode = [int]$_.Exception.Response.StatusCode.value__ }
            catch { $statusCode = 0 }

            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $raw = $reader.ReadToEnd()
                    if (-not [string]::IsNullOrWhiteSpace($raw)) {
                        try { $content = $raw | ConvertFrom-Json }
                        catch { $content = $raw }
                    }
                }
            }
            catch {
            }
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Body = $content
        }
    }
}

function Pause-Step {
    Write-Host ""
    Read-Host "Press Enter to continue"
}

function Run-ParallelGet {
    $id = Read-Host "Product ID"
    $attempts = Read-Host "How many parallel GET requests"
    if (-not [int]::TryParse($attempts, [ref]$null)) {
        Write-Host "Invalid attempts value."
        return
    }

    $count = [int]$attempts
    if ($count -gt $MaxAttempts) {
        Write-Host "Attempts limited to $MaxAttempts."
        $count = $MaxAttempts
    }

    $poolSize = [Math]::Min([Math]::Max($count, 1), $MaxConcurrency)
    $runspacePool = [runspacefactory]::CreateRunspacePool(1, $poolSize)
    $runspacePool.Open()

    $invocations = @()
    $script = {
        param($attempt, $parallelHost, $productId)

        try {
            $request = [System.Net.HttpWebRequest]::Create("$parallelHost/api/products/$productId")
            $request.Method = "GET"
            $response = [System.Net.HttpWebResponse]$request.GetResponse()
            $status = [int]$response.StatusCode
            $response.Close()

            [pscustomobject]@{ Attempt = $attempt; Status = $status }
        }
        catch [System.Net.WebException] {
            $status = 0
            if ($_.Exception.Response) {
                try { $status = [int]$_.Exception.Response.StatusCode }
                catch { $status = 0 }
            }

            [pscustomobject]@{ Attempt = $attempt; Status = $status }
        }
    }

    try {
        foreach ($i in 1..$count) {
            $ps = [powershell]::Create()
            $ps.RunspacePool = $runspacePool
            [void]$ps.AddScript($script).AddArgument($i).AddArgument($HostUrl).AddArgument($id)
            $handle = $ps.BeginInvoke()
            $invocations += [pscustomobject]@{ PowerShell = $ps; Handle = $handle }
        }

        $results = foreach ($item in $invocations) {
            try { $item.PowerShell.EndInvoke($item.Handle) }
            finally { $item.PowerShell.Dispose() }
        }
    }
    finally {
        $runspacePool.Close()
        $runspacePool.Dispose()
    }

    $grouped = $results | Group-Object Status | Sort-Object Name
    Write-Host "\nParallel GET results:"
    foreach ($g in $grouped) {
        Write-Host "Status $($g.Name): $($g.Count)"
    }
}

function Run-ParallelCreateSameSku {
    $sku = Read-Host "SKU for all parallel create requests"
    $name = Read-Host "Name"
    $priceInput = Read-Host "Price"
    $attempts = Read-Host "How many parallel create requests"

    $price = 0.0
    if (-not [double]::TryParse($priceInput, [ref]$price)) {
        Write-Host "Invalid price value."
        return
    }

    $count = 0
    if (-not [int]::TryParse($attempts, [ref]$count)) {
        Write-Host "Invalid attempts value."
        return
    }

    if ($count -gt $MaxAttempts) {
        Write-Host "Attempts limited to $MaxAttempts."
        $count = $MaxAttempts
    }

    $body = @{
        sku = $sku
        name = $name
        price = $price
    } | ConvertTo-Json

    $poolSize = [Math]::Min([Math]::Max($count, 1), $MaxConcurrency)
    $runspacePool = [runspacefactory]::CreateRunspacePool(1, $poolSize)
    $runspacePool.Open()

    $invocations = @()
    $script = {
        param($attempt, $parallelHost, $parallelBody)

        try {
            $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($parallelBody)
            $request = [System.Net.HttpWebRequest]::Create("$parallelHost/api/products")
            $request.Method = "POST"
            $request.ContentType = "application/json"
            $request.ContentLength = $bodyBytes.Length

            $stream = $request.GetRequestStream()
            $stream.Write($bodyBytes, 0, $bodyBytes.Length)
            $stream.Close()

            $response = [System.Net.HttpWebResponse]$request.GetResponse()
            $status = [int]$response.StatusCode

            $id = $null
            $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
            $raw = $reader.ReadToEnd()
            $reader.Close()
            $response.Close()

            if (-not [string]::IsNullOrWhiteSpace($raw)) {
                try {
                    $json = $raw | ConvertFrom-Json
                    if ($json -and $json.id) {
                        $id = [int]$json.id
                    }
                }
                catch {
                }
            }

            [pscustomobject]@{ Attempt = $attempt; Status = $status; Id = $id }
        }
        catch [System.Net.WebException] {
            $status = 0
            if ($_.Exception.Response) {
                try { $status = [int]$_.Exception.Response.StatusCode }
                catch { $status = 0 }
            }

            [pscustomobject]@{ Attempt = $attempt; Status = $status; Id = $null }
        }
    }

    try {
        foreach ($i in 1..$count) {
            $ps = [powershell]::Create()
            $ps.RunspacePool = $runspacePool
            [void]$ps.AddScript($script).AddArgument($i).AddArgument($HostUrl).AddArgument($body)
            $handle = $ps.BeginInvoke()
            $invocations += [pscustomobject]@{ PowerShell = $ps; Handle = $handle }
        }

        $results = foreach ($item in $invocations) {
            try { $item.PowerShell.EndInvoke($item.Handle) }
            finally { $item.PowerShell.Dispose() }
        }
    }
    finally {
        $runspacePool.Close()
        $runspacePool.Dispose()
    }

    $grouped = $results | Group-Object Status | Sort-Object Name
    Write-Host "\nParallel CREATE results (same SKU):"
    foreach ($g in $grouped) {
        Write-Host "Status $($g.Name): $($g.Count)"
    }

    $created = @($results | Where-Object { $_.Status -eq 201 })
    if ($created.Count -gt 0) {
        Write-Host "Created product id: $($created[0].Id)"
    }
}

Write-Host "Host: $HostUrl"
Write-Host "Tip: Open monitor at $HostUrl/monitor"

while ($true) {
    Write-Host ""
    Write-Host "===== Interactive Cache Scenario ====="
    Write-Host "1) GET product by id"
    Write-Host "2) Parallel GET (same id)"
    Write-Host "3) CREATE product"
    Write-Host "4) Parallel CREATE with same SKU"
    Write-Host "5) UPDATE product"
    Write-Host "0) Exit"

    $choice = Read-Host "Choose action"

    switch ($choice) {
        "1" {
            $id = Read-Host "Product ID"
            $res = Invoke-Api -Method GET -Url "$HostUrl/api/products/$id"
            Write-Host "Status=$($res.StatusCode)"
            if ($res.Body) { $res.Body | ConvertTo-Json -Depth 10 }
            Pause-Step
        }
        "2" {
            Run-ParallelGet
            Pause-Step
        }
        "3" {
            $sku = Read-Host "SKU"
            $name = Read-Host "Name"
            $priceInput = Read-Host "Price"
            $price = 0.0
            if (-not [double]::TryParse($priceInput, [ref]$price)) {
                Write-Host "Invalid price value."
            }
            else {
                $body = @{ sku = $sku; name = $name; price = $price } | ConvertTo-Json
                $res = Invoke-Api -Method POST -Url "$HostUrl/api/products" -Body $body
                Write-Host "Status=$($res.StatusCode)"
                if ($res.Body) { $res.Body | ConvertTo-Json -Depth 10 }
            }
            Pause-Step
        }
        "4" {
            Run-ParallelCreateSameSku
            Pause-Step
        }
        "5" {
            $id = Read-Host "Product ID"
            $name = Read-Host "New name"
            $priceInput = Read-Host "New price"
            $price = 0.0
            if (-not [double]::TryParse($priceInput, [ref]$price)) {
                Write-Host "Invalid price value."
            }
            else {
                $body = @{ name = $name; price = $price } | ConvertTo-Json
                $res = Invoke-Api -Method PUT -Url "$HostUrl/api/products/$id" -Body $body
                Write-Host "Status=$($res.StatusCode)"
                if ($res.Body) { $res.Body | ConvertTo-Json -Depth 10 }
            }
            Pause-Step
        }
        "0" {
            break
        }
        default {
            Write-Host "Invalid choice."
            Pause-Step
        }
    }
}
