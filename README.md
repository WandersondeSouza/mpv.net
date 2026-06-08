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

O fork tambem inclui um projeto MSIX/WAP em `src/MpvNet.Pacote/MpvNet.Pacote.wapproj` para publicacao na Microsoft Store.
O fluxo dedicado de build esta em `src/Tools/build-store-package.ps1` e usa os assets do proprio repositorio.
O fluxo unico de publicacao para humanos e CI e `src/Tools/publish-store-package.ps1`.
O arquivo real `src/MpvNet.Pacote/Packaging.Distribution.props` nao deve ser versionado; use o exemplo e variaveis de ambiente para segredos. Se o `.pfx` tiver senha, use `MPVNET_STORE_CERTIFICATE_PASSWORD`.
Se houver um `.pfx` local comum ao lado de `src/MpvNet.Pacote` ou em `src/`, o script tenta encontra-lo automaticamente.
Use `src/Tools/find-store-certificate.ps1` para ver qual certificado local seria usado.
Para uso local direto, `src/Tools/build-store-package-auto.ps1` tenta descobrir o certificado e gera o pacote em um unico comando.

A build 64bit-v3 do mpv/libmpv é o padrão nos scripts de build/release do fork. A variante normal ainda pode ser selecionada explicitamente para compatibilidade com CPUs x64 mais antigas; veja [Build e release](docs/developer/build-release.md).

## Logs

Resumo rápido:

- `Error` sempre vai para arquivo.
- `Info` e `Debug` só vão para arquivo em build de diagnóstico.
- Logs diários são retidos por 3 dias.
- Limpeza de `Cache` e `Temp` também usa 3 dias.

Detalhes em [Logs de diagnostico](docs/logging.md).

## Para agentes

Antes de alterar código ou documentação, leia:

1. `AGENTS.md`
2. `README.md`
3. `docs/manual.md`
4. `docs/guia-operacional.md`
5. `.ai/README.md`
6. `docs/developer/architecture.md` quando a mudança for ampla
7. a documentação da área tocada em `docs/developer/`

Os arquivos em `.ai/` existem para orientar tarefas recorrentes do fork, mas não substituem a análise do código atual.


