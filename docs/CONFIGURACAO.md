# Configuração do MPV.NET Media Player

Este documento resume onde o fork procura as configurações e quais arquivos o usuário normalmente edita.

## Ordem de busca

1. `MPVNET_HOME`
2. `portable_config`
3. `%APPDATA%\mpv.net`

## Arquivos principais

- `mpv.conf`: opções do mpv
- `mpvnet.conf`: opções do mpv.net
- `input.conf`: atalhos, ações e menu
- `menu.conf`: menu OSD do `select.lua`, gerado automaticamente no idioma ativo quando ausente
- `global-input.conf`: atalhos globais
- `settings.xml`: estado interno salvo pelo aplicativo
- `theme.conf`: tema visual

## Estrutura portátil

Se `portable_config` existir ao lado de `mpvnet.exe`, o player usa essa pasta como configuração principal.

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

## Observações

- `mpv.conf` deve continuar compatível com o mpv.
- `input.conf` pode ser usado para atalhos personalizados e menu.
- O menu OSD do `Ctrl+p` usa `menu.conf` quando existe; o mpv.net gera esse arquivo com rótulos localizados se ele ainda não existir.
- Scripts e opções de scripts ficam em `scripts/` e `script-opts/`.
- O exemplo de `thumbfast` do fork fica em `docs/exemplos/thumbfast.conf`.
