# Mapa Inicial do Projeto mpv.net

## Objetivo

Este documento organiza o entendimento estrutural do projeto mpv.net.

---

# Visão geral

O projeto pode ser dividido em:

1. inicialização;
2. integração com mpv/libmpv;
3. interface gráfica;
4. sistema de comandos;
5. sistema de configuração;
6. scripts/extensões;
7. temas;
8. empacotamento/release.

---

# Áreas principais

## Inicialização

Responsável por:

- iniciar aplicação;
- carregar ambiente;
- resolver configurações;
- criar janela principal.

---

## Interface gráfica

Responsável por:

- janela;
- menus;
- controles;
- fullscreen;
- overlays;
- temas.

---

## Integração com mpv/libmpv

Responsável por:

- reprodução;
- propriedades;
- comandos;
- sincronização;
- eventos.

---

## Configuração

Arquivos principais:

- mpv.conf;
- mpvnet.conf;
- input.conf.

---

## Scripts e extensões

Tipos:

- Lua;
- JavaScript;
- extensões .NET.

---

# Áreas críticas para manutenção

## Compatibilidade

Compatibilidade com mpv é prioridade.

## Fullscreen

Mudanças podem impactar:

- input;
- menu;
- OSC;
- comportamento multi-monitor.

## Configuração

Mudanças podem quebrar instalações antigas.

---

# Pendências futuras

Este documento deve futuramente incluir:

- estrutura real de pastas;
- projetos `.csproj`;
- solução `.sln`;
- entry points reais;
- classes principais;
- fluxo de eventos;
- dependências externas.
