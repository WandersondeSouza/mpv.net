# Configuração do MPV.NET Media Player

Encontre a pasta ativa e o arquivo que precisa editar.

## Ordem de busca

1. `--config-dir=...`, se informado
2. `MPVNET_HOME`, se apontar para uma pasta existente
3. `portable_config` ao lado do executável, em instalações sem identidade MSIX
4. `%LOCALAPPDATA%\mpv.net`, ou a raiz privada de dados da Microsoft Store

O mpv e o frontend usam a mesma pasta para configuração, estado e tema.
Use `mpvnet.exe --diagnose-paths` para conferir os caminhos efetivos. O comando
não cria pastas. Veja os detalhes de cada modo em [Configuração técnica](developer/configuration.md).

## Arquivos principais

- `mpv.conf`: opções do mpv
- `mpvnet.conf`: opções do mpv.net
- `input.conf`: atalhos, ações e menu
- `menu.conf`: menu OSD do `select.lua`, gerado automaticamente no idioma ativo quando ausente
- `global-input.conf`: atalhos globais
- `settings.xml`: estado interno salvo pelo aplicativo
- `theme.conf`: tema visual

## Estrutura portátil

Extraia o ZIP completo. Crie `portable_config` ao lado de `mpvnet.exe` se o diretório ainda não existir.

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

## Observações

Em **Cache → Cache automático de streaming**, use Sim/Não para a política
automática. O equivalente em `mpvnet.conf` é `automatic-network-cache=yes`
(padrão) ou `no`. Os perfis antigos são migrados na leitura; ao salvar uma
alteração no editor, um backup preserva o formato anterior. Desligar a política
não muda as opções nativas do `mpv.conf`. Veja [limites e migração](developer/mpv-integration.md#cache-automático-por-item).

- `mpv.conf` deve continuar compatível com o mpv.
- `input.conf` pode ser usado para atalhos personalizados e menu.
- Se `input.conf` usar menu personalizado, os itens base `Open Files...` e `About mpv.net` continuam sendo preservados quando estiverem ausentes.
- O menu OSD do `Ctrl+p` usa `menu.conf` quando existe; o mpv.net gera esse arquivo com rótulos localizados se ele ainda não existir.
- Scripts e opções de scripts ficam em `scripts/` e `script-opts/`.
- Consulte os [exemplos de configuração](exemplos/) e o [exemplo de `thumbfast`](exemplos/thumbfast.conf).
