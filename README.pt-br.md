# mpv.net

> Tradução e adaptação em português brasileiro para o fork de manutenção.

O **mpv.net** é um player de mídia para Windows com uma interface gráfica moderna.

O player é baseado no popular projeto [mpv](https://mpv.io). O mpv.net foi projetado para ser altamente compatível com o mpv, portanto quase todos os recursos do mpv também estão disponíveis aqui.

A documentação oficial do mpv continua sendo uma referência importante. As diferenças específicas do mpv.net são documentadas no manual do projeto.

---

# Principais recursos

## Interface gráfica moderna

Interface visual para Windows com suporte a temas e customizações.

## Compatibilidade com mpv

O projeto busca manter compatibilidade com comandos, opções e comportamento geral do mpv.

## Linha de comando

O mpv.net suporta grande parte da interface de linha de comando do mpv.

## Saída de vídeo de alta qualidade

Suporte a recursos avançados de vídeo, como escalonamento, gerenciamento de cor, HDR, interpolação e aceleração por GPU.

## Controlador na tela

Controles modernos para reprodução diretamente sobre o vídeo.

## Decodificação por GPU

Suporte a aceleração de decodificação de vídeo via APIs do FFmpeg, como DXVA2.

## Baseado em libmpv

O mpv.net utiliza a libmpv, uma API C criada para permitir que o mpv seja integrado a outras aplicações.

---

# Recursos em comum com o mpv

- Scripts Lua e JavaScript;
- arquivos de configuração simples;
- IPC JSON para controle externo;
- linha de comando;
- saída de status, erro e debug no terminal;
- aceleração de vídeo;
- suporte a extensões de navegador;
- busca rápida;
- inicialização rápida;
- suporte a vídeo, áudio e imagens;
- decodificadores embutidos;
- suporte a streaming com yt-dlp;
- carregamento de áudio e legenda externos;
- captura de tela;
- internacionalização.

---

# Recursos específicos do mpv.net

- interface gráfica moderna;
- temas customizáveis;
- menu de contexto customizável;
- editor pesquisável de configuração;
- editor pesquisável de atalhos;
- atalhos globais;
- API de extensões para linguagens .NET;
- suporte a enfileirar arquivos pelo Windows Explorer.

---

# Documentação

## Para usuários

- [Manual original](docs/manual.md)
- [Suporte](docs/manual.md#support)
- [Download](docs/manual.md#download)

## Para desenvolvedores e mantenedores

- [Arquitetura do projeto](docs/developer/architecture-ptbr.md)
- [Guia de build](docs/developer/build-ptbr.md)
- [Sistema de configuração](docs/developer/configuration-system-ptbr.md)
- [Sistema de comandos](docs/developer/commands-ptbr.md)
- [Guia de contribuição](docs/contributing-ptbr.md)
- [Orientações para agentes de IA](AGENTS.md)

---

# Objetivo deste fork

Este fork tem como objetivo:

1. preservar o projeto;
2. melhorar a documentação;
3. criar documentação em português brasileiro;
4. preparar o repositório para uso com agentes de IA/Codex;
5. facilitar manutenção futura;
6. estudar melhorias graduais sem quebrar compatibilidade.

---

# Aviso importante

Este documento é uma tradução/adaptação inicial. Para detalhes técnicos completos e comportamento oficial, consulte também o README original e o manual em inglês.
