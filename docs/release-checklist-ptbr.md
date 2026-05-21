# Checklist de Release

## Objetivo

Garantir que releases futuras sejam feitas de forma segura e organizada.

---

# Build

- [ ] solução compila;
- [ ] dependências resolvidas;
- [ ] build release executado;
- [ ] sem erros críticos.

---

# Testes manuais

- [ ] player abre;
- [ ] vídeo reproduz;
- [ ] áudio reproduz;
- [ ] imagens abrem;
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
- [ ] documentação atualizada;
- [ ] README atualizado;
- [ ] riscos documentados.

---

# Publicação

- [ ] artefatos gerados;
- [ ] workflow manual `.github/workflows/release-packages.yml` validado, quando a release for feita pelo GitHub Actions;
- [ ] download automatico de FFmpeg concluido;
- [ ] download automatico de libmpv concluido;
- [ ] download automatico de yt-dlp concluido;
- [ ] `Locale` gerado a partir de `lang/po`;
- [ ] ZIP portatil contem `libmpv-2.dll`, `MediaInfo.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe`;
- [ ] `MediaInfo.dll` copiado de `src/Native/win-x64/MediaInfo.dll`;
- [ ] `mpvnet.com` fornecido por `-MpvNetComFile`, existente no build output ou baixado pelo script;
- [ ] versão validada;
- [ ] release publicada;
- [ ] links revisados.
