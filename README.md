# mpv.net

Fork de manutenção do mpv.net para Windows, focado em compatibilidade com mpv/libmpv, documentação objetiva e mudanças pequenas e verificadas.

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
- [Atalhos](docs/ATALHOS.md)
- [Próximos trabalhos](docs/proximos-trabalhos.md)
- [Arquitetura](docs/developer/architecture-ptbr.md)
- [Auditoria de código](docs/developer/source-audit-ptbr.md)
- [Mapa do projeto](docs/developer/project-map-ptbr.md)
- [Mapa de classes](docs/developer/class-map-ptbr.md)
- [Artefatos de IA](.ai/README.md)

## Uso rápido

```powershell
mpvnet.exe "C:\Videos\filme.mp4"
mpvnet.exe "C:\Listas\iptv.m3u"
mpvnet.exe "https://example.com/live/index.m3u8"
```

O mpv.net aceita arquivos locais, playlists e URLs de streaming compatíveis com o mpv.

## Para agentes

Antes de alterar código ou documentação, leia:

1. `AGENTS.md`
2. `README.md`
3. `docs/manual.md`
4. `docs/guia-operacional.md`
5. `docs/proximos-trabalhos.md`
6. `.ai/README.md`
7. `docs/developer/architecture-ptbr.md` e os mapas técnicos relacionados quando a mudança for ampla
8. a documentação da área tocada em `docs/developer/`

Os arquivos em `.ai/` existem para orientar tarefas recorrentes do fork, mas não substituem a análise do código atual.
