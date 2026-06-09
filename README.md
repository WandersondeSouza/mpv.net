# MPV.NET Media Player

Fork de manutenção do **MPV.NET Media Player** para Windows, baseado no mpv.net e focado em compatibilidade com mpv/libmpv, documentação objetiva e mudanças pequenas e verificadas.

## Prioridades

- manter o comportamento existente;
- preservar compatibilidade com arquivos de configuração e comandos do mpv;
- documentar uso, configuração, build e release do fork;
- corrigir bugs pequenos sem refatorações amplas;
- manter a manutenção do repositório simples e rastreável.

## Documentação principal

- [Manual](docs/manual.md)
- [Configuração](docs/CONFIGURACAO.md)
- [Guia operacional](docs/guia-operacional.md)
- [Logs de diagnóstico](docs/logging.md)
- [Atalhos](docs/ATALHOS.md)
- [Arquitetura técnica](docs/developer/architecture.md)
- [Configuração técnica](docs/developer/configuration.md)
- [Integração com mpv/libmpv](docs/developer/mpv-integration.md)
- [Interface Windows](docs/developer/windows-ui.md)
- [Build e release](docs/developer/build-release.md)
- [Localização](docs/developer/localization.md)
- [Artefatos de IA](.ai/README.md)

## Uso rápido

```powershell
mpvnet.exe "C:\Videos\filme.mp4"
mpvnet.exe "C:\Listas\iptv.m3u"
mpvnet.exe "https://example.com/live/index.m3u8"
```

O mpv.net aceita arquivos locais, playlists e URLs de streaming compatíveis com o mpv.

## Build

Veja [Build e release](docs/developer/build-release.md).

## Logs

Veja [Logs de diagnostico](docs/logging.md).

## Para agentes

Veja [AGENTS.md](AGENTS.md) e [Artefatos de IA](.ai/README.md).


