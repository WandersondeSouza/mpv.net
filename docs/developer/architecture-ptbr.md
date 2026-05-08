# Arquitetura do Projeto mpv.net

## Objetivo

Este documento tem como objetivo ajudar mantenedores, desenvolvedores e agentes de IA a entender a arquitetura geral do projeto mpv.net.

O foco inicial é mapear responsabilidades, áreas críticas e fluxo geral da aplicação.

---

# Visão Geral

O mpv.net é um frontend Windows para o mpv/libmpv.

O projeto utiliza o motor do mpv para reprodução multimídia e implementa uma interface gráfica moderna para Windows.

A prioridade arquitetural do projeto é:

1. compatibilidade com mpv;
2. simplicidade de configuração;
3. alta capacidade de customização;
4. suporte a scripts e extensões;
5. integração com recursos modernos do Windows.

---

# Principais Camadas

## 1. Inicialização da aplicação

Responsável por:

- iniciar a aplicação;
- carregar configurações;
- validar ambiente;
- iniciar integração com mpv/libmpv;
- criar janela principal.

---

## 2. Camada de Interface Gráfica

Responsável por:

- janela principal;
- controles visuais;
- menus;
- overlays;
- temas;
- integração com mouse e teclado.

Mudanças nessa área podem impactar:

- fullscreen;
- atalhos;
- DPI;
- comportamento multi-monitor.

---

## 3. Integração com mpv/libmpv

Camada crítica do projeto.

Responsável por:

- comunicação com libmpv;
- propriedades;
- comandos;
- eventos;
- estado de reprodução.

Essa área deve preservar compatibilidade máxima com o comportamento original do mpv.

---

## 4. Sistema de configuração

Responsável por:

- carregar mpv.conf;
- carregar mpvnet.conf;
- carregar input.conf;
- resolver pasta de configuração;
- salvar preferências.

---

## 5. Sistema de comandos

Responsável por:

- atalhos;
- comandos internos;
- integração terminal;
- comandos específicos do mpv.net.

---

## 6. Sistema de scripts e extensões

Responsável por:

- scripts Lua;
- scripts JavaScript;
- extensões .NET.

---

# Áreas consideradas sensíveis

## Integração com libmpv

Mudanças podem quebrar:

- reprodução;
- sincronização;
- eventos;
- compatibilidade com scripts.

## Configuração

Mudanças podem quebrar instalações existentes.

## UI

Mudanças podem impactar:

- usabilidade;
- fullscreen;
- OSC;
- menu de contexto.

---

# Prioridades deste fork

Este fork inicialmente prioriza:

1. documentação técnica;
2. documentação em português brasileiro;
3. preparação para agentes de IA;
4. entendimento da arquitetura;
5. melhoria gradual da manutenção.

---

# Próximos documentos recomendados

- build-ptbr.md
- commands-ptbr.md
- configuration-system-ptbr.md
- ui-ptbr.md
- mpv-integration-ptbr.md
- contributing-ptbr.md
