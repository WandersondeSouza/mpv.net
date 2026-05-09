# Modo portátil do mpv.net

Este documento explica como usar o mpv.net em modo portátil real.

## Diferença entre ZIP e portátil real

A versão em ZIP não usa instalador. Porém, isso não significa que todas as configurações ficarão automaticamente dentro da pasta do programa.

Para que o mpv.net grave as configurações dentro da própria pasta do player, é necessário criar uma pasta chamada:

```text
portable_config
```

ao lado do arquivo:

```text
mpvnet.exe
```

## Estrutura recomendada

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

## Para que serve cada item

- `mpv.conf`: configurações gerais do mpv/mpv.net.
- `input.conf`: atalhos de teclado, mouse e comandos do menu.
- `scripts/`: scripts Lua ou JavaScript usados pelo mpv.
- `script-opts/`: arquivos de configuração dos scripts.

## Quando a pasta portable_config não existe

Se a pasta `portable_config` não existir, o mpv.net pode usar a pasta de configuração do usuário no Windows, por exemplo:

```text
C:\Users\<usuario>\AppData\Roaming\mpv.net
```

Isso pode confundir usuários que esperam que a versão ZIP seja totalmente portátil.

## Recomendação para o fork

Nas futuras versões deste fork, o ideal é que o pacote ZIP portátil já venha com uma estrutura básica:

```text
portable_config/
portable_config/mpv.conf
portable_config/input.conf
portable_config/scripts/
portable_config/script-opts/
```

Assim o usuário entende imediatamente onde colocar suas configurações.

## Tarefa futura para o Codex

Verificar o script de release:

```text
src/Tools/release-mpv.net.ps1
```

E avaliar se a pasta `portable_config` pode ser incluída automaticamente no pacote ZIP portátil.
