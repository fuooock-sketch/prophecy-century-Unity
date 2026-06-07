param(
    [string]$MapPath = "Assets/Resources/Data/world_maps.json",
    [string]$MapId = "mvp_3_layer_map",
    [int]$MaxMovePoints = 4
)

$ErrorActionPreference = "Stop"

$maps = Get-Content -LiteralPath $MapPath -Encoding UTF8 -Raw | ConvertFrom-Json
$map = $maps | Where-Object { $_.id -eq $MapId } | Select-Object -First 1
if ($null -eq $map) {
    throw "Map '$MapId' was not found in $MapPath."
}

$nodesById = @{}
$map.nodes | ForEach-Object {
    if ([string]::IsNullOrWhiteSpace($_.id)) {
        throw "Map '$MapId' contains a node with an empty id."
    }

    $nodesById[$_.id] = $_
}

if (-not $nodesById.ContainsKey($map.startNodeId)) {
    throw "Map '$MapId' start node '$($map.startNodeId)' does not exist."
}

$bossNodes = @($map.nodes | Where-Object { $_.type -eq "boss" })
if ($bossNodes.Count -eq 0) {
    throw "Map '$MapId' does not contain a boss node."
}

$edges = @{}
$map.connections | ForEach-Object {
    if (-not $nodesById.ContainsKey($_.fromNodeId)) {
        throw "Connection source '$($_.fromNodeId)' does not exist."
    }

    if (-not $nodesById.ContainsKey($_.toNodeId)) {
        throw "Connection target '$($_.toNodeId)' does not exist."
    }

    $fromNode = $nodesById[$_.fromNodeId]
    $toNode = $nodesById[$_.toNodeId]
    if ($toNode.layer -ne $fromNode.layer + 1) {
        throw "Connection '$($_.fromNodeId)' -> '$($_.toNodeId)' does not move to the next layer."
    }

    if (-not $edges.ContainsKey($_.fromNodeId)) {
        $edges[$_.fromNodeId] = New-Object System.Collections.Generic.List[string]
    }

    $edges[$_.fromNodeId].Add($_.toNodeId)
}

$queue = New-Object System.Collections.Generic.Queue[object]
$queue.Enqueue([pscustomobject]@{
    NodeId = $map.startNodeId
    Steps = 0
    Path = @($map.startNodeId)
})

$visited = @{}
$reachableBoss = $null
while ($queue.Count -gt 0 -and $null -eq $reachableBoss) {
    $current = $queue.Dequeue()
    if ($visited.ContainsKey($current.NodeId) -and $visited[$current.NodeId] -le $current.Steps) {
        continue
    }

    $visited[$current.NodeId] = $current.Steps
    if ($nodesById[$current.NodeId].type -eq "boss") {
        $reachableBoss = $current
        break
    }

    if (-not $edges.ContainsKey($current.NodeId)) {
        continue
    }

    foreach ($nextNodeId in $edges[$current.NodeId]) {
        $nextPath = @($current.Path) + $nextNodeId
        $queue.Enqueue([pscustomobject]@{
            NodeId = $nextNodeId
            Steps = $current.Steps + 1
            Path = $nextPath
        })
    }
}

if ($null -eq $reachableBoss) {
    throw "Map '$MapId' has no path from '$($map.startNodeId)' to a boss node."
}

if ($reachableBoss.Steps -gt $MaxMovePoints) {
    throw "Boss path requires $($reachableBoss.Steps) moves, exceeding max move points $MaxMovePoints."
}

"World map MVP validation OK: map=$MapId, boss=$($reachableBoss.NodeId), moves=$($reachableBoss.Steps), path=$($reachableBoss.Path -join ' -> ')"
