# Checklist de release

Checklist curto para releases do fork.

## Antes da publicação

- build local concluído;
- `src/BuildVersion.props` revisado;
- changelog atualizado;
- release branch/tag conferidos;
- dependências nativas validadas.

## Pacote portátil

- ZIP gerado;
- `mpvnet.exe`, `libmpv-2.dll`, `MediaInfo.dll`, FFmpeg, `yt-dlp.exe` e `Locale` presentes;
- `portable_config` incluído;
- `validate-native-dependencies.ps1` sem erro.

## UI e compatibilidade

- player abre;
- vídeo e áudio reproduzem;
- fullscreen funciona;
- menu e atalhos funcionam;
- configuração persiste;
- temas funcionam;
- caminhos longos não quebram.

## Publicação

- instalador gerado;
- release publicada;
- links conferidos.
