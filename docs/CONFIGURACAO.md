# Configuracao do mpv.net

Este documento explica, de forma inicial, onde ficam e para que servem os principais arquivos de configuracao do mpv.net.

O mpv.net e baseado no mpv/libmpv. Por isso, muitas opcoes seguem o comportamento do mpv original.

## mpv.conf

O arquivo `mpv.conf` guarda configuracoes gerais do player.

Exemplos comuns:

```text
pause=yes
volume=100
hwdec=auto-safe
save-position-on-quit=yes
```

Cada linha normalmente define uma opcao do mpv. Quando houver duvida sobre uma opcao, consulte o manual oficial do mpv.

## input.conf

O arquivo `input.conf` guarda atalhos de teclado, mouse e comandos.

Exemplo:

```text
Space cycle pause
f cycle fullscreen
Right seek 2
Left seek -2
```

Se a mesma tecla aparecer mais de uma vez, pode haver conflito. Em muitos casos, a ultima definicao encontrada passa a prevalecer.

## Scripts

Scripts permitem estender o comportamento do mpv/mpv.net.

Normalmente ficam na pasta:

```text
scripts/
```

O mpv aceita scripts em Lua e JavaScript, dependendo do recurso usado e da instalacao.

## script-opts

A pasta `script-opts/` guarda arquivos de configuracao de scripts.

Exemplo:

```text
script-opts/
  thumbfast.conf
```

Nem todo script precisa de um arquivo em `script-opts`. Quando precisar, siga a documentacao do proprio script.

### thumbfast

Para usar `thumbfast`, coloque o script em:

```text
scripts/thumbfast.lua
```

E a configuracao opcional em:

```text
script-opts/thumbfast.conf
```

O exemplo deste fork fica em:

```text
docs/exemplos/thumbfast.conf
```

No mpv.net v7, o `thumbfast` tem suporte direto. Nao configure `mpv_path` por padrao; deixe essa opcao apenas como fallback para versoes antigas ou casos indicados pela documentacao atual do script.

## Instalacao normal

Na instalacao normal, os arquivos de configuracao podem ficar na pasta do usuario do Windows, por exemplo:

```text
C:\Users\<usuario>\AppData\Roaming\mpv.net
```

Esse caminho pode variar conforme a versao, configuracao ou modo de execucao.

## Versao portatil

Na versao portatil real, crie a pasta `portable_config` ao lado do `mpvnet.exe`:

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

Veja tambem: [Modo portatil](PORTATIL.md).

## Usando os exemplos deste repositorio

Este fork possui exemplos iniciais em:

- [docs/exemplos/mpv.conf](exemplos/mpv.conf)
- [docs/exemplos/input.conf](exemplos/input.conf)
- [docs/exemplos/thumbfast.conf](exemplos/thumbfast.conf)

Use esses arquivos como ponto de partida. Eles nao substituem a documentacao oficial do mpv, mas ajudam a criar uma configuracao basica.

## Cuidados com configuracoes duplicadas

- Evite repetir a mesma opcao varias vezes no `mpv.conf`.
- Evite repetir a mesma tecla varias vezes no `input.conf`.
- Ao testar uma configuracao, altere uma coisa por vez.
- Se algo parar de funcionar, renomeie temporariamente o arquivo alterado e teste novamente.

Configuracoes duplicadas dificultam a investigacao de bugs, porque nem sempre fica claro qual linha esta prevalecendo.
