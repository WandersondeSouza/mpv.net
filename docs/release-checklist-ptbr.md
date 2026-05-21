# Checklist de Release

## Objetivo

Garantir que releases futuras sejam feitas de forma segura e organizada.

Status em 2026-05-21: itens marcados foram validados em dry run local com `-SkipGitHubRelease -SkipInstaller`, sem gerar instalador e sem publicar a release.

---

# Build

- [x] solução compila;
- [x] dependências resolvidas;
- [ ] build release executado;
- [x] sem erros críticos.

---

# Testes manuais

- [x] player abre;
- [x] vídeo reproduz;
- [x] áudio reproduz;
- [x] imagens abrem;
- [ ] fullscreen funciona;
- [ ] menu funciona;
- [ ] atalhos funcionam;
- [ ] configuração persiste;
- [ ] temas funcionam.

---

# Compatibilidade

- [ ] compatibilidade com mpv preservada;
- [ ] scripts continuam funcionando;
- [ ] input.conf compatível;
- [ ] mpv.conf compatível;
- [ ] mpvnet.conf compatível.

---

# Documentação

- [ ] changelog atualizado;
- [x] documentação atualizada;
- [ ] README atualizado;
- [ ] riscos documentados.

---

# Publicação

- [x] artefatos gerados;
- [ ] workflow manual `.github/workflows/release-packages.yml` validado, quando a release for feita pelo GitHub Actions;
- [x] download automatico de FFmpeg concluido;
- [x] download automatico de libmpv concluido;
- [x] download automatico de yt-dlp concluido;
- [x] `Locale` gerado a partir de `lang/po`;
- [x] ZIP portatil contem `libmpv-2.dll`, `MediaInfo.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe`;
- [x] `MediaInfo.dll` copiado de `src/Native/win-x64/MediaInfo.dll`;
- [x] `mpvnet.com` fornecido por `-MpvNetComFile`, existente no build output ou baixado pelo script;
- [x] versão validada;
- [ ] release publicada;
- [ ] links revisados.
