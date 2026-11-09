param([switch]$Final)
$ErrorActionPreference = 'Stop'
$auditRoot = 'C:/Dev/OmniEurope/OmniEurope.Documents/.claude/audit/2026-11-09'
$auditArtifacts = "$auditRoot/artefacts"
$auditInventory = @(Get-Content -LiteralPath "$auditArtifacts/inventory.json" -Raw | ConvertFrom-Json)
$auditStates = @(Get-ChildItem -LiteralPath $auditArtifacts -Filter 'state-*.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$auditRemaining = $auditInventory.Count - $auditStates.Count
if ($Final -and $auditRemaining -ne 0) { throw "Review incomplete: $auditRemaining files remaining." }
if ($Final -and -not (Test-Path -LiteralPath "$auditArtifacts/metrics/EVIDENCE_CHECK.json")) { throw 'Independent evidence check is required.' }
if ($Final) { $auditCheck = Get-Content -LiteralPath "$auditArtifacts/metrics/EVIDENCE_CHECK.json" -Raw | ConvertFrom-Json; if ($auditCheck.result -ne 'PASS' -or $auditCheck.remaining -ne 0) { throw 'Independent evidence check did not pass with zero remaining files.' } }
$auditFindings = @(Get-ChildItem -LiteralPath $auditArtifacts -Filter 'findings-*.json' | Where-Object BaseName -NE 'findings-consolidated' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } | Sort-Object id)
foreach ($finding in $auditFindings) {
    $finding.severity = switch ($finding.severity.ToLowerInvariant()) {
        'critique' { 'critical' }
        'élevé' { 'high' }
        'moyen' { 'medium' }
        'faible' { 'low' }
        default { $finding.severity.ToLowerInvariant() }
    }
    if ($finding.severity -notin @('critical','high','medium','low')) { throw "Unknown severity: $($finding.severity)" }
}
if (@($auditFindings | Group-Object id | Where-Object Count -GT 1).Count -gt 0) { throw 'Duplicate finding identifiers.' }
$auditSeverityOrder = @{ critical=0; high=1; medium=2; low=3 }
$auditFindings = @($auditFindings | Sort-Object @{Expression={$auditSeverityOrder[$_.severity]}}, id)
function Encode-Audit([object]$Value) { [Net.WebUtility]::HtmlEncode([string]$Value) }
function Get-AuditDimension([object]$Finding) {
    $auditDim = $Finding.dimension.ToLowerInvariant()
    if ($auditDim -match 'sécurité|security|confidential') { return 'security' }
    if ($auditDim -match 'cohérence|homogéné|déterminisme') { return 'coherence' }
    if ($auditDim -match 'convention|protection|kit') { return 'conventions' }
    if ($auditDim -match 'fiabilité|tests') { return 'tests' }
    if ($auditDim -match 'performance') { return 'performance' }
    if ($auditDim -match 'architecture') { return 'architecture' }
    if ($auditDim -match 'documentation') { return 'docs' }
    if ($auditDim -match 'dépendance') { return 'dependencies' }
    return 'correctness'
}
$auditItems = @($auditFindings | ForEach-Object {
    $auditFinding = $_
    $auditText = '<strong>' + (Encode-Audit $auditFinding.title) + '</strong>'
    foreach ($auditField in @(@('consequence','Conséquence'),@('evidence','Preuve'),@('recommendation','Intervention proposée'),@('acceptance',"Critère d'acceptation"),@('proofKind','Nature de la preuve'))) {
        $auditValue = $auditFinding.($auditField[0])
        if ($auditValue) { $auditText += '<p><b>' + $auditField[1] + ' :</b> ' + (Encode-Audit $auditValue) + '</p>' }
    }
    [ordered]@{ id=$auditFinding.id; dim=(Get-AuditDimension $auditFinding); sev=$auditFinding.severity; status='finding'; module=$auditFinding.module; size=''; src='audit'; cat='tech'; file=($auditFinding.file+':'+$auditFinding.line); text=$auditText }
})
ConvertTo-Json -InputObject $auditFindings -Depth 8 | Set-Content -LiteralPath "$auditArtifacts/findings-consolidated.json" -Encoding utf8
$auditData = (ConvertTo-Json -InputObject $auditItems -Depth 8 -Compress).Replace('</','<\/')
$auditStatus = if ($Final) { 'Revue achevée, limites de vérification explicites' } else { 'Revue en cours, résultats provisoires' }
$auditModuleRows = ($auditInventory | Group-Object module | ForEach-Object {
    $auditReviewed = @($auditStates | Where-Object module -EQ $_.Name).Count
    '<tr><td>' + $_.Name + '</td><td>' + $auditReviewed + '/' + $_.Count + '</td><td>Lecture ou inspection complète, sans ancien cache</td></tr>'
}) -join ''
$auditCoherenceRows = @(Get-Content -LiteralPath "$auditArtifacts/AUDIT_COHERENCE.md" | Where-Object { $_ -match '^\| ' -and $_ -notmatch '^\| Concept comparé' } | ForEach-Object {
    $cells = $_.Trim().Trim('|').Split('|')
    if ($cells.Count -eq 3) {
        '<tr>' + (($cells | ForEach-Object {
            $cell = Encode-Audit $_.Trim()
            $cell = [regex]::Replace($cell, '`([^`]+)`', '<code>$1</code>')
            $cell = [regex]::Replace($cell, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
            '<td>' + $cell + '</td>'
        }) -join '') + '</tr>'
    }
}) -join ''
$auditHeader = @"
<p class="subtitle">Audit neuf du 9 novembre 2026 · develop · 2594b1d · $auditStatus</p>
<div class="audit-intro"><h2>Un paquet NuGet, des contrats à rapprocher</h2>
<p>Priorité à la cohérence des API publiques et des comportements entre formats. Chaque constat associe conséquence, preuve observée, intervention proposée et critère d’acceptation. Aucun code corrigé, aucune publication.</p>
<p><b>Revue :</b> $($auditStates.Count)/$($auditInventory.Count) fichiers, $auditRemaining restant(s). Mode intégral, zéro réutilisation de cache. Le suivi de tous les fichiers ne certifie pas toutes les entrées possibles.</p>
<p><b>Exécution réelle :</b> compilation Release Windows et Linux sans avertissement ni erreur ; 2 809 tests rapides et 50 stress réussis, aucun ignoré. Deux DLL couvertes : 97,44 % des lignes, 87,55 % des branches ; garde CRAP réussi sur 4 035 méthodes. Paquet 1.1.0 vérifié sans dépendance ; consommateur avec cache neuf, 14 assertions générales et 12 fontes réussies.</p>
<p><b>API publiques :</b> 304 types recensés, matrice de 22 concepts. Variations certaines : ContentType/MediaType et Sheet/Worksheet ; les différences justifiées par le format sont distinguées des défauts reproduits. Aucune uniformisation cassante n'a été appliquée.</p>
<p><b>Sécurité et limites :</b> Gitleaks sur 118 commits et l'arbre suivi, aucun secret détecté ; scan NuGet sans paquet vulnérable rapporté. Fichiers ignorés hors scan, pas de SAST général ni de certification indépendante PDF/A/cryptographique, pas de preuve Native AOT, WebAssembly ou Office externe. Framework erroné du graphe Roslyn écarté au profit des manifests et de la compilation.</p>
<p>Les cases servent à sélectionner les constats, pas à les déclarer corrigés. Les données restent enregistrées localement dans ce navigateur.</p>
<p><b>Contrôle du rapport :</b> syntaxe JavaScript vérifiée statiquement. Affichage non vérifié : le navigateur autonome refuse le protocole file: ; aucun contournement tenté.</p>
<p><a href="AUDIT_SUMMARY.md">Synthèse</a> · <a href="artefacts/AUDIT_COHERENCE.md">Matrice de cohérence</a> · <a href="artefacts/metrics/VALIDATION.md">Commandes et résultats</a> · <a href="artefacts/AUDIT_DEPENDENCIES.md">Dépendances</a></p>
<details><summary>Comparer les noms, options et contrats des API publiques</summary><div class="matrix"><table><thead><tr><th>Concept</th><th>Contrats et conséquence pour le consommateur</th><th>Appréciation</th></tr></thead><tbody>$auditCoherenceRows</tbody></table></div></details>
<details><summary>Couverture des modules et historique</summary><table><thead><tr><th>Module</th><th>Fichiers</th><th>Preuve de revue</th></tr></thead><tbody>$auditModuleRows</tbody></table><p>L'audit du 30 octobre est conservé comme historique ; ses conclusions et mesures ne servent pas de preuve actuelle. Chaque ligne de la présente revue renvoie à une lecture ou inspection de cette passe.</p></details></div>
"@
$auditCss = @'
h1 { background:linear-gradient(135deg,var(--red),var(--orange));-webkit-background-clip:text;-webkit-text-fill-color:transparent; }
.audit-intro { border:1px solid var(--border);background:var(--surface);border-radius:.75rem;padding:1.4rem;margin:1.5rem 0 2rem; }
.audit-intro h2 { font-size:1.15rem;margin-bottom:.65rem; }.audit-intro p { margin:.6rem 0; }
.audit-intro a { color:var(--accent); }.audit-intro details { margin-top:1rem; }.audit-intro summary { cursor:pointer;font-weight:600; }
table { width:100%;border-collapse:collapse;margin-top:.8rem;font-size:.85rem; } th,td { text-align:left;padding:.5rem;border-bottom:1px solid var(--border); }
.matrix { overflow-x:auto; }.matrix table { min-width:600px; }.matrix td { vertical-align:top; }.matrix code { overflow-wrap:anywhere; font-size:.82rem; }
.badge.critical { background:#3a0a0a;color:#ff4040; }.badge.high { background:#3a1a0a;color:var(--red); }.badge.medium { background:#3a2e0e;color:var(--orange); }.badge.low { background:#1a2a3a;color:var(--accent); }
.item-file { font-family:monospace;font-size:.75rem;color:var(--text-muted);margin-top:.5rem;overflow-wrap:anywhere; }.item-text p { margin:.55rem 0; }.item { cursor:default; }
.item:focus-visible { outline:2px solid var(--accent);outline-offset:3px; }.item-badges button { background:transparent;cursor:pointer; }.item label { cursor:pointer; }.select-item { margin-top:.3rem;accent-color:var(--accent); }
@media(max-width:650px) { body { padding:1rem; }.item { flex-wrap:wrap; }.item-badges { flex-direction:row;flex-wrap:wrap; }.item-text { flex-basis:100%; }.stat { min-width:100px;padding:.65rem; } }
'@
$auditJs = @'
const auditDimensions={security:'Sécurité et confidentialité',coherence:'Cohérence du paquet NuGet',correctness:'Exactitude',architecture:'Architecture',quality:'Qualité',performance:'Performance',tests:'Fiabilité et tests',conventions:'Conventions et protections',leftovers:'Résidus',completeness:'Complétude',docs:'Documentation',authenticity:'Authenticité',dependencies:'Dépendances',metrics:'Métriques'};
const auditSeverity={critical:'Critique',high:'Élevé',medium:'Moyen',low:'Faible',info:'Information'};
const auditRank={critical:0,high:1,medium:2,low:3,info:4};
function auditFilter(type,value){activeFilter=activeFilter&&activeFilter.type===type&&activeFilter.value===value?null:{type,value};render();updateFilterBtns();}
function filterModule(m){auditFilter('module',m);}
getFiltered=function(){return items.filter(i=>{if(activeFilter&&i[activeFilter.type]!==activeFilter.value)return false;if(checkedFilter==='checked'&&!checkedIds.has(i.id))return false;if(checkedFilter==='unchecked'&&checkedIds.has(i.id))return false;return !searchTerm||[i.id,i.file,i.module,i.text.replace(/<[^>]*>/g,'')].join(' ').toLowerCase().includes(searchTerm);}).sort((a,b)=>auditRank[a.sev]-auditRank[b.sev]||a.id.localeCompare(b.id));};
renderItem=function(i){const checked=checkedIds.has(i.id);return `<article class="item${checked?' checked-item':''}" data-id="${i.id}"><div class="item-badges"><button class="item-id" title="Copier l’identifiant" onclick="copyId('${i.id}')">${i.id}</button><span class="badge ${i.sev}">${auditSeverity[i.sev]}</span><span class="badge">${i.module}</span><label><input class="select-item" type="checkbox" ${checked?'checked':''} aria-label="Sélectionner ${i.id}" onchange="toggleCheck('${i.id}')"> Sélectionner</label></div><div class="item-text">${i.text}<div class="item-file">${i.file}</div></div></article>`;};
render=function(){const filtered=getFiltered();let html='';for(const [dim,label] of Object.entries(auditDimensions)){const group=filtered.filter(i=>i.dim===dim);if(!group.length)continue;html+=`<section class="section" id="${dim}"><div class="section-header"><h2>${label}</h2><span class="count-badge">${group.length}</span></div><div class="items">${group.map(renderItem).join('')}</div></section>`;}document.getElementById('content').innerHTML=html||'<p>Aucun constat pour ces filtres.</p>';const stat=(value,label)=>`<div class="stat"><span class="stat-num">${value}</span><span class="stat-label">${label}</span></div>`;document.getElementById('stats').innerHTML=stat(filtered.length,'Constats affichés')+['critical','high','medium','low'].map(s=>stat(filtered.filter(i=>i.sev===s).length,auditSeverity[s])).join('')+stat(filtered.filter(i=>checkedIds.has(i.id)).length,'Sélectionnés');document.getElementById('cnt-checked').textContent='('+filtered.filter(i=>checkedIds.has(i.id)).length+')';buildTimeline();};
document.querySelector('.toc').innerHTML='<h3>Dimensions avec constats</h3>'+Object.entries(auditDimensions).filter(([d])=>items.some(i=>i.dim===d)).map(([d,label])=>`<a href="#${d}">${label} (${items.filter(i=>i.dim===d).length})</a>`).join('');
document.querySelector('.filters').innerHTML='<button class="filter-btn active" onclick="filterAll()">Tout</button>'+Object.entries(auditSeverity).filter(([s])=>s!=='info').map(([s,label])=>`<button class="filter-btn" data-type="sev" data-value="${s}" onclick="auditFilter('sev','${s}')">${label}</button>`).join('')+[...new Set(items.map(i=>i.module))].sort().map(m=>`<button class="filter-btn" data-type="module" data-value="${m}" onclick="filterModule('${m}')">${m}</button>`).join('')+'<button class="filter-btn" id="btn-unchecked" onclick="filterChecked(\'unchecked\')">Non sélectionnés</button><button class="filter-btn" id="btn-checked" onclick="filterChecked(\'checked\')">Sélectionnés</button><input class="search-box" type="search" aria-label="Rechercher dans les constats" placeholder="Rechercher un constat ou un fichier…" oninput="filterSearch(this.value)">';
updateFilterBtns=function(){document.querySelectorAll('.filter-btn').forEach(b=>b.classList.remove('active'));if(!activeFilter&&!checkedFilter)document.querySelector('.filter-btn').classList.add('active');if(activeFilter){const b=document.querySelector(`[data-type="${activeFilter.type}"][data-value="${activeFilter.value}"]`);if(b)b.classList.add('active');}if(checkedFilter)document.getElementById('btn-'+checkedFilter).classList.add('active');};
const rejectButton=document.querySelector('[onclick="copyRejectCmd()"]');if(rejectButton)rejectButton.remove();
'@
$auditShell = Get-Content -LiteralPath 'C:/Users/Woluwe/.claude/commands/assets/report-shell.html' -Raw
$auditShell = $auditShell.Replace('<!--SLOT:TITLE-->','OmniEurope.Documents : audit 360').Replace('<!--SLOT:HEADER-->',$auditHeader).Replace('<!--SLOT:EXTRA_CSS-->',$auditCss).Replace('<!--SLOT:BODY-->','').Replace('<!--SLOT:DATA-->',$auditData.TrimStart('[').TrimEnd(']')).Replace('<!--SLOT:EXTRA_JS-->',$auditJs).Replace('{{DATE}}','2026-11-09').Replace('{{STORAGE_KEY}}','audit-checked-omnieurope-documents-2026-11-09')
[IO.File]::WriteAllText("$auditRoot/audit-report.html", $auditShell.Replace([string][char]0x2014,'-'), [Text.UTF8Encoding]::new($false))
"Report=$auditRoot/audit-report.html Findings=$($auditFindings.Count) Reviewed=$($auditStates.Count) Remaining=$auditRemaining"
