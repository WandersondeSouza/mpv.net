# Checklist de Release

## Objetivo

Garantir que releases futuras sejam feitas de forma segura e organizada.

Status em 2026-05-22: os itens marcados abaixo foram validados localmente para a versao `7.1.2.2`, incluindo ZIP portatil, instalador e validacao de DLLs nativas. A release `v7.1.2.2` foi publicada no GitHub, mas os testes manuais de UI ainda precisam ser fechados.

Use este checklist como fonte de verdade da release. Se um item for validado em outra rodada, marque apenas esse item e registre a evidencia no documento relacionado.

Itens marcados com `[x]` abaixo já podem ser considerados fechados para a versao `7.1.2.2` e podem sair desta lista quando a proxima versao for consolidada, desde que o changelog e a documentacao associada continuem coerentes.

---

# Preparacao

- [x] `git status --short --branch` revisado antes da rodada de validacao;
- [x] build local de `src\MpvNet.Windows\MpvNet.Windows.csproj` executado sem erros;
- [x] dependencias de build resolvidas;
- [x] `src/BuildVersion.props` revisado com a versao da release;
- [x] tela `Help > About mpv.net` revisada com versao, identidade do fork e link do repositorio;
- [x] branch/tag de release revisados antes da publicacao;
- [x] changelog da versao revisado.

---

# Pacote portatil

- [x] dry run local executado com `src\Tools\release-mpv.net.ps1 .\src .\artifacts\release-native-installer-test -SkipGitHubRelease`;
- [x] artefatos gerados;
- [x] download automatico de FFmpeg concluido;
- [x] download automatico de libmpv concluido;
- [x] download automatico de yt-dlp concluido;
- [x] download automatico de MediaInfo concluido a partir da MediaArea oficial;
- [x] DLLs Microsoft/.NET/WPF validadas a partir do publish self-contained;
- [x] `Locale` gerado a partir de `lang/po`;
- [x] ZIP portatil contem `mpvnet.exe`, `libmpv-2.dll`, `MediaInfo.dll`, DLLs `.NET/WPF`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe`, `mpvnet.com`, `Locale/` e `portable_config/`;
- [x] `MediaInfo.dll` baixado/validado pelo script `download-native-dependencies.ps1`;
- [x] `mpvnet.com` fornecido por `-MpvNetComFile`, existente no build output ou baixado pelo script;
- [x] versao validada no nome do ZIP gerado;
- [x] `test-native-dependencies.ps1` validou publish, pasta portatil e ZIP.
- [x] ZIP portatil final da versao `7.1.2.2` validado com as DLLs nativas obrigatorias.

---

# Smoke test do ZIP extraido

- [x] player abre;
- [x] video reproduz;
- [x] audio reproduz;
- [x] imagens abrem;
- [ ] fullscreen funciona;
- [ ] menu funciona;
- [ ] atalhos funcionam;
- [ ] configuracao persiste;
- [ ] temas funcionam;
- [ ] smoke test com comparacao visual em modo escuro concluido.

---

# Compatibilidade

- [ ] compatibilidade com mpv preservada em teste manual;
- [ ] scripts continuam funcionando em pacote extraido;
- [ ] `input.conf` compativel;
- [ ] `mpv.conf` compativel;
- [ ] `mpvnet.conf` compativel;
- [ ] `thumbfast` validado no layout portátil real;
- [ ] caminhos longos validados sem regressao.

---

# Instalador e publicacao

- [x] build release executado para publicacao final;
- [x] instalador gerado localmente;
- [x] log do Inno Setup confirmou inclusao das DLLs nativas obrigatorias a partir do publish;
- [x] instalador executado e validado localmente;
- [ ] workflow manual `.github/workflows/release-packages.yml` validado, quando a release for feita pelo GitHub Actions;
- [x] release publicada;
- [x] links revisados.

---

# Documentacao

- [x] documentacao de build, modo portatil, dependencias nativas e checklist alinhada ao dry run validado;
- [x] changelog da release final `7.1.2.2` atualizado;
- [ ] README revisado quando houver mudanca de comportamento visivel para usuario;
- [ ] riscos documentados quando algum item manual ficar pendente.

---

# Fora do escopo desta fase

Nao iniciar durante a estabilizacao pos-primeira versao:

- uosc;
- LUT;
- AI upscaling;
- IPTV/media center;
- mudancas profundas em yt-dlp;
- refatoracoes amplas sem bug comprovado.
