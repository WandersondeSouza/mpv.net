# Atalhos e input.conf

Este documento explica como funcionam os atalhos do mpv.net.

O mpv.net utiliza o arquivo:

```text
input.conf
```

para controlar atalhos de teclado, mouse e comandos do menu.

## Onde fica o input.conf

### Instalação normal

Normalmente o arquivo pode ficar em:

```text
C:\Users\<usuario>\AppData\Roaming\mpv.net
```

### Versão portátil

Na versão portátil:

```text
portable_config/input.conf
```

## Exemplo simples

```text
Right seek 2
Left seek -2
Space cycle pause
f cycle fullscreen
```

## Problemas comuns

### 1. Atalho duplicado

Se a mesma tecla for definida mais de uma vez, apenas a última definição pode funcionar.

Exemplo:

```text
Right seek 2
Right seek 10
```

Nesse caso, o último comando pode sobrescrever o primeiro.

## 2. Diferença entre versões antigas e novas

Alguns usuários relataram diferenças entre versões antigas do mpv.net e versões mais novas.

Em alguns casos, atalhos antigos continuam funcionando, mas o editor visual pode não refletir corretamente as mudanças.

## 3. Editor de atalhos

O editor de atalhos do mpv.net ajuda a editar o `input.conf`, mas algumas configurações avançadas ainda podem precisar de edição manual.

## Como restaurar os atalhos

Uma forma simples de restaurar os atalhos é:

1. fechar o mpv.net;
2. renomear o arquivo `input.conf`;
3. abrir novamente o mpv.net;
4. criar um novo arquivo padrão.

## Tarefa futura para o Codex

Investigar melhorias no editor de atalhos:

- detectar teclas duplicadas;
- mostrar conflitos antes de salvar;
- melhorar sincronização entre interface gráfica e arquivo `input.conf`;
- validar atalhos inválidos.
