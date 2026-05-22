# mpv.net - fork de manutenção

Este repositório é um fork de manutenção do **mpv.net**, um player de mídia para Windows com interface gráfica moderna, baseado no **mpv/libmpv**.

O projeto original foi criado e mantido por **Frank Skare / stax76**. Este fork respeita esse trabalho e tem como prioridade preservar o funcionamento existente, melhorar a documentação em português brasileiro e corrigir problemas pequenos de forma cuidadosa.

Grandes mudanças de arquitetura não são prioridade neste momento. A regra principal é manter compatibilidade com o mpv sempre que possível e evitar alterações amplas sem necessidade.

## Objetivos do fork

- Preservar o comportamento atual do mpv.net.
- Documentar melhor o uso, a configuração, o build e a manutenção do projeto.
- Facilitar o uso e a contribuição em português brasileiro.
- Corrigir bugs simples e bem delimitados.
- Preparar o repositório para manutenção assistida por agentes de IA.

## Para usuarios

- [Manual do mpv.net](docs/manual.md)
- [Manual oficial do mpv](https://mpv.io/manual/master/)
- [Modo portátil](docs/PORTATIL.md)
- [Configuração](docs/CONFIGURACAO.md)
- [Atalhos e input.conf](docs/ATALHOS.md)
- [Exemplo de `mpv.conf`](docs/exemplos/mpv.conf)
- [Exemplo de `input.conf`](docs/exemplos/input.conf)
- [Exemplo de `thumbfast.conf`](docs/exemplos/thumbfast.conf)

## Para mantenedores

- [Plano de trabalho do Codex](docs/PLANO-CODEX.md)
- [Roadmap do fork](docs/ROADMAP.md)
- [Build e release](docs/BUILD.md)
- [Checklist de release](docs/release-checklist-ptbr.md)
- [Caminhos longos no Windows](docs/CAMINHOS-LONGOS.md)
- [Guia de contribuição](docs/contributing-ptbr.md)
- [Documentação técnica](docs/developer/project-map-ptbr.md)

## Para agentes de IA

Antes de qualquer alteracao automatizada, leia:

1. [AGENTS.md](AGENTS.md)
2. [Skill de manutenção do fork](.ai/skills/mpvnet-maintainer.md)
3. [Plano de trabalho do Codex](docs/PLANO-CODEX.md)
4. [Prompt de próximas melhorias](.ai/prompts/next-improvements.md)

Os artefatos em `.ai/` ajudam a orientar análises e mudanças pequenas, mas não substituem a leitura do código atual.

## Sobre compatibilidade

O **mpv.net** é uma interface gráfica para Windows baseada no [mpv](https://mpv.io). A compatibilidade com o mpv é uma característica central do projeto: a maior parte dos recursos, opções de configuração, scripts e comandos do mpv também se aplica ao mpv.net.

Para documentação técnica completa do comportamento herdado do mpv, consulte o [manual oficial do mpv](https://mpv.io/manual/master/) e o [manual do mpv.net](docs/manual.md).

---

## README original

![GitHub closed pull requests](https://img.shields.io/github/issues-pr-closed/stax76/mpv.net) ![GitHub closed issues](https://img.shields.io/github/issues-closed/stax76/mpv.net) ![GitHub All Releases](https://img.shields.io/github/downloads/stax76/mpv.net/total) ![GitHub tag (latest by date)](https://img.shields.io/github/tag-date/stax76/mpv.net) ![GitHub stars](https://img.shields.io/github/stars/stax76/mpv.net)

🎞 mpv.net
==========

mpv.net is a media player for Windows with a modern GUI.

The player is based on the popular [mpv](https://mpv.io) media player.
mpv.net is designed to be mpv compatible, almost all mpv features are available,
this means the official [mpv manual](https://mpv.io/manual/master/) applies to mpv.net,
differences are documented in the [mpv.net manual](docs/manual.md#differences-compared-to-mpv).

#### Graphical User Interface

Modern GUI with customizable color themes.


#### Command Line Interface

mpv.net supports mpv's command-line interface.


#### High quality video output

Video output that is capable of many features loved by videophiles,
such as video scaling with popular high quality algorithms,
color management, frame timing, interpolation, HDR, and more.


#### On Screen Controller

Play controls with a modern flat design.


#### GPU video decoding

Leverages the FFmpeg hwaccel APIs to support DXVA2 video decoding acceleration.


#### Based on libmpv

mpv.net is based on libmpv which offers a straightforward C API that
was designed from the ground up to make mpv usable as a library and
facilitate easy integration into other applications.
mpv is like VLC not based on DirectShow or Media Foundation.


Table of contents
-----------------

- [Features](#features-that-mpv-and-mpvnet-have-in-common)
- [Support](#support)
- [Download](#download)
- [Manual](#manual)
- [Screenshots](#screenshots)
- [Contributing](#contributing)


Features that mpv and mpv.net have in common
--------------------------------------------

- Lua and JavaScript Scripting ([awesome-mpv lists a large collection of available user scripts](https://github.com/stax76/awesome-mpv))
- Simple config files that are easy to read and edit
- JSON IPC to control the player from external programs
- On Screen Controller (OSC, play control buttons) with modern flat design
- Command Line Interface
- Started from a terminal, status, error and debug output is printed on the terminal
- DXVA2 video decoding acceleration
- Video output capable of features loved by videophiles, such as video scaling with popular high quality algorithms, color management, frame timing, interpolation, HDR, and more
- Browser extensions to start mpv.net from the browser
- Fast seek performance
- Fast startup performance
- Usable as video player, audio player and image viewer with a wide range of supported formats
- Built-in decoders, no external codecs have to be installed
- Built-in media streaming (requires yt-dlp to be installed)
- External audio and subtitle files can be loaded manually or automatically
- Screenshot feature
- Internationalization using gettext and transifex


Features exclusive to mpv.net
----------------------------

- Very high degree of mpv compatibility, almost all mpv features are available
- Modern graphical user interface with customizable color themes
- Customizable context menu
- Searchable config editor
- Searchable input (shortcut keys) editor
- Global keyboard shortcuts
- Extension API for .NET languages (C#, VB.NET and F#)
- Files can be enqueued from File Explorer


## [Support](docs/manual.md#support)

[Support section of the manual.](docs/manual.md#support)


## [Download](docs/manual.md#download)

[Download section of the manual.](docs/manual.md#download)


## [Manual](docs/manual.md)

[The mpv.net documentation.](docs/manual.md)


## [Contributing](docs/contributing-ptbr.md)

[Contributing guide for this fork.](docs/contributing-ptbr.md)


Screenshots
-----------

#### Main Window

![Main Window](docs/img/Main.webp)


#### Context Menu

![Context Menu](docs/img/Menu.webp)


#### Config Editor

Searchable config editor as alternative to edit the conf file manually.

![](docs/img/ConfEditor.webp)


#### Terminal

![](docs/img/Terminal.webp)


Other projects from me
----------------------

A list of my other projects can be found here:

https://stax76.github.io/software-list
