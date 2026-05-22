# Configuração do mpv.net

Este documento explica, de forma inicial, onde ficam e para que servem os principais arquivos de configuração do mpv.net.

O mpv.net é baseado no mpv/libmpv. Por isso, muitas opções seguem o comportamento do mpv original.

## mpv.conf

O arquivo `mpv.conf` guarda configurações gerais do player.

Exemplos comuns:

```text
pause=yes
reset-on-next-file=pause
volume=100
hwdec=auto-safe
save-position-on-quit=yes
```

Cada linha normalmente define uma opção do mpv. Quando houver dúvida sobre uma opção, consulte o manual oficial do mpv.

Se você usa `pause=yes` e quer que cada novo item da playlist também volte a iniciar pausado, mantenha `reset-on-next-file=pause`. Sem essa opção, depois que o usuário tira o player da pausa, o mpv pode manter `pause=no` ao avançar para o próximo arquivo.

Este fork também pode criar um perfil chamado `[iptv-media-center]` no `mpv.conf`. Ele fica inativo no uso normal e só é aplicado quando outro aplicativo inicia o player com:

```text
--profile=iptv-media-center
```

O perfil existe para integrar o pacote com o IPTV Media Center sem transformar essas opções em padrão global do mpv.net.

## input.conf

O arquivo `input.conf` guarda atalhos de teclado, mouse e comandos.

Exemplo:

```text
Space cycle pause
f cycle fullscreen
Right seek 2
Left seek -2
```

Se a mesma tecla aparecer mais de uma vez, pode haver conflito. Em muitos casos, a última definição encontrada passa a prevalecer.

## Scripts

Scripts permitem estender o comportamento do mpv/mpv.net.

Normalmente ficam na pasta:

```text
scripts/
```

O mpv aceita scripts em Lua e JavaScript, dependendo do recurso usado e da instalação.

## script-opts

A pasta `script-opts/` guarda arquivos de configuração de scripts.

Exemplo:

```text
script-opts/
  thumbfast.conf
```

Nem todo script precisa de um arquivo em `script-opts`. Quando precisar, siga a documentação do próprio script.

### thumbfast

Para usar `thumbfast`, coloque o script em:

```text
scripts/thumbfast.lua
```

E a configuração opcional em:

```text
script-opts/thumbfast.conf
```

O exemplo deste fork fica em:

```text
docs/exemplos/thumbfast.conf
```

No mpv.net v7, o `thumbfast` tem suporte direto. Não configure `mpv_path` por padrão; deixe essa opção apenas como fallback para versões antigas ou casos indicados pela documentação atual do script.

Em validação de 2026-05-21, o `thumbfast.lua` real carregou corretamente a partir de `portable_config/scripts/`, leu `portable_config/script-opts/thumbfast.conf` e emitiu mensagens `thumbfast-info` sem erros. Para ver thumbnails na prática, ainda é necessário usar uma UI/script compatível que consuma essas mensagens.

## Instalação normal

Na instalação normal, os arquivos de configuração podem ficar na pasta do usuário do Windows, por exemplo:

```text
C:\Users\<usuario>\AppData\Roaming\mpv.net
```

Esse caminho pode variar conforme a versão, configuração ou modo de execução.

Quando essa pasta é usada e ainda não existe `mpv.conf`, o mpv.net cria um arquivo inicial com o perfil `[iptv-media-center]`. Se o usuário já possui `mpv.conf`, o arquivo existente não é sobrescrito.

## Versão portátil

Na versão portátil real, crie a pasta `portable_config` ao lado do `mpvnet.exe`:

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

Veja também: [Modo portátil](PORTATIL.md).

## Usando os exemplos deste repositório

Este fork possui exemplos iniciais em:

- [docs/exemplos/mpv.conf](exemplos/mpv.conf)
- [docs/exemplos/input.conf](exemplos/input.conf)
- [docs/exemplos/thumbfast.conf](exemplos/thumbfast.conf)

Use esses arquivos como ponto de partida. Eles não substituem a documentação oficial do mpv, mas ajudam a criar uma configuração básica.

## Cuidados com configurações duplicadas

- Evite repetir a mesma opção várias vezes no `mpv.conf`.
- Evite repetir a mesma tecla várias vezes no `input.conf`.
- Ao testar uma configuração, altere uma coisa por vez.
- Se algo parar de funcionar, renomeie temporariamente o arquivo alterado e teste novamente.

Configurações duplicadas dificultam a validação de bugs, porque nem sempre fica claro qual linha está prevalecendo.
