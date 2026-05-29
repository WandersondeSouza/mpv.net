# Fluxo de Configuração do mpv.net

## Objetivo

Documentar o fluxo de carregamento e persistência de configuração.

> Documento inicial baseado na documentação existente.

---

# Ordem esperada de resolução

1. `MPVNET_HOME`;
2. `portable_config`;
3. `%APPDATA%\\mpv.net`.

---

# Arquivos principais

## mpv.conf

Configurações do mpv.

---

## mpvnet.conf

Configurações específicas do frontend.

---

## input.conf

Atalhos e comandos.

---

# Fluxo esperado

1. Aplicação inicia.
2. Pasta de configuração é localizada.
3. Arquivos são lidos.
4. Configurações são aplicadas.
5. UI e libmpv recebem estado inicial.
6. Alterações podem ser persistidas.

---

# Áreas críticas

## Compatibilidade

Mudanças podem quebrar usuários existentes.

## Persistência

Mudanças podem impedir salvamento correto.

## Scripts

Scripts podem depender de opções específicas.

---

# Próxima etapa

Durante auditoria real:

- localizar classes reais;
- localizar parser;
- localizar persistência;
- localizar fallback;
- validar fluxo real.
