# Mapa Inicial de Classes e Responsabilidades

## Objetivo

Organizar entendimento das principais responsabilidades arquiteturais do projeto.

> Documento inicial. Os nomes reais devem ser confirmados durante auditoria completa do código.

---

# Áreas esperadas

## Aplicação principal

Responsável por:

- startup;
- ciclo de vida;
- argumentos;
- inicialização geral.

---

## Janela principal

Responsável por:

- renderização da UI;
- fullscreen;
- overlays;
- interação do usuário.

---

## Integração libmpv

Responsável por:

- comunicação com libmpv;
- propriedades;
- comandos;
- eventos.

---

## Configuração

Responsável por:

- leitura;
- persistência;
- resolução de caminhos;
- compatibilidade.

---

## Comandos/Input

Responsável por:

- atalhos;
- input.conf;
- menu de contexto;
- parser de comandos.

---

## Scripts e extensões

Responsável por:

- scripts Lua;
- scripts JavaScript;
- extensões .NET.

---

# Próxima etapa

Durante análise real do código, este documento deve ser atualizado com:

- nomes reais das classes;
- namespaces;
- dependências;
- acoplamentos;
- pontos de extensão;
- riscos arquiteturais.
