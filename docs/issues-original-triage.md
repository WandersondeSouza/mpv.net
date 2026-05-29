# Issues do projeto original: triagem para o fork

Este documento cruza issues abertos do repositório original `mpvnet-player/mpv.net` com o estado atual deste fork.

## Critério

- `novo`: ainda falta implementar ou validar no fork.
- `parcial`: existe suporte parcial, mas o comportamento pedido pelo issue não está garantido.
- `coberto`: o fork já possui a base técnica do recurso ou a correção.

## Itens mais úteis para trabalhar no fork

### Novo

- `#744` True lut support
  - Estado no fork: não encontrei suporte completo do recurso.
  - Próximo passo: validar se o fluxo atual de `editor_conf.txt` e do player cobre o comportamento real ou só expõe opções.

- `#730` My profile-cond causes the shortcut keys to stop working
  - Estado no fork: não encontrei correção específica.
  - Próximo passo: reproduzir com `profile-cond`, revisar carga de `input.conf` e event flow de bindings.

### Parcial

- `#738` The ability to by default open a window maximized not fullscreen
  - Estado no fork: existe suporte a `window-maximized`.
  - Lacuna: o issue pede comportamento padrão de abertura, não só observação do estado depois que a janela já está em uso.

- `#729` mpv.net audio is very quiet
  - Estado no fork: existe lógica de volume e persistência, mas isso não prova que o sintoma do issue foi eliminado.
  - Próximo passo: validar reprodução real e comparar com `mpv.exe`.

### Coberto ou já resolvido

- `#735` mpvnet can't open videos with long path, while mpv.exe can
  - O fork já normaliza caminho longo com `\\?\` em `Player.ConvertFilePath`.

- `#747` Lauching via udp:// is no more working
  - O fork já trata `udp://` como protocolo de streaming em `FileTypes.StreamingProtocols`.

- `#731` Is the translation service not open now?
  - Isso é sobre o serviço externo Transifex, não sobre bug do fork.
  - O que cabe ao fork é documentar o fluxo atual de tradução e indicar o estado do serviço externo.

## Comentário curto para issues já cobertos

Use este texto quando a correção já existir no fork:

> No fork atual do mpv.net este caso já está coberto ou parcialmente coberto. No caso específico de `X`, já existe suporte/correção no código do fork. Se ainda houver algum sintoma, ele provavelmente depende de validação adicional do cenário específico, mas a base técnica já está implementada aqui.

## Comentário curto para suporte parcial

Use este texto quando o fork tem base técnica, mas não o comportamento completo:

> No fork atual do mpv.net existe suporte parcial para este cenário, mas o comportamento pedido ainda não está garantido de ponta a ponta. A base técnica já existe, então o próximo passo é fechar a validação do fluxo específico.

