# Checklist de Release

## Objetivo

Garantir que releases futuras sejam feitas de forma segura e organizada.

Status em 2026-05-21: itens marcados foram validados em dry run local com `-SkipGitHubRelease -SkipInstaller`, sem gerar instalador e sem publicar a release. Itens sem marca continuam pendentes de validacao real antes de anunciar uma release como completa.

Use este checklist como fonte de verdade da release. Se um item for validado em outra rodada, marque apenas esse item e registre a evidencia no documento relacionado.

---

# Preparacao

- [x] `git status --short --branch` revisado antes da rodada de validacao;
- [x] build local de `src\MpvNet.Windows\MpvNet.Windows.csproj` executado sem erros;
- [x] dependencias de build resolvidas;
- [ ] branch/tag de release revisados antes da publicacao;
- [ ] changelog da versao revisado.

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
- [ ] temas funcionam.

---

# Compatibilidade

- [ ] compatibilidade com mpv preservada em teste manual;
- [ ] scripts continuam funcionando em pacote extraido;
- [ ] `input.conf` compativel;
- [ ] `mpv.conf` compativel;
- [ ] `mpvnet.conf` compativel.

---

# Instalador e publicacao

- [ ] build release executado para publicacao final;
- [x] instalador gerado localmente;
- [x] log do Inno Setup confirmou inclusao das DLLs nativas obrigatorias a partir do publish;
- [ ] instalador instalado em maquina limpa ou perfil isolado;
- [ ] workflow manual `.github/workflows/release-packages.yml` validado, quando a release for feita pelo GitHub Actions;
- [ ] release publicada;
- [ ] links revisados.

---

# Documentacao

- [x] documentacao de build, modo portatil, dependencias nativas e checklist alinhada ao dry run validado;
- [ ] changelog da release final atualizado;
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
