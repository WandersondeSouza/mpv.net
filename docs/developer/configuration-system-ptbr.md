# Sistema de Configuração do mpv.net

## Objetivo

Documentar como o mpv.net localiza, carrega e utiliza arquivos de configuração.

---

# Visão geral

O mpv.net utiliza uma abordagem compatível com o mpv, baseada principalmente em arquivos de configuração simples.

Isso facilita:

- customização;
- portabilidade;
- scripts;
- backup;
- automação.

---

# Ordem de resolução da pasta de configuração

Segundo a documentação atual:

1. variável de ambiente `MPVNET_HOME`;
2. `portable_config` no diretório do executável;
3. `%APPDATA%\\mpv.net`.

Essa ordem é crítica para compatibilidade.

---

# Arquivos principais

## mpv.conf

Responsável pelas configurações do mpv.

Exemplos:

- vídeo;
- áudio;
- fullscreen;
- HDR;
- interpolação;
- escalonamento.

---

## mpvnet.conf

Responsável pelas opções específicas do mpv.net.

Exemplos:

- comportamento da janela;
- temas;
- recursos específicos da interface;
- comportamento de instância única.

---

## input.conf

Responsável pelos atalhos e comandos.

Pode conter:

- atalhos de teclado;
- comandos personalizados;
- customizações do menu de contexto.

---

# Pastas importantes

## scripts

Scripts Lua e JavaScript.

---

## script-opts

Configuração dos scripts.

---

## extensions

Extensões .NET.

---

# Áreas críticas

## Compatibilidade

Mudanças no sistema de configuração podem quebrar instalações existentes.

## Migração de versões

Mudanças de sintaxe devem considerar compatibilidade retroativa.

## Performance

Leitura excessiva de configuração pode impactar startup.

---

# Recomendações para manutenção

1. Não alterar nomes de arquivos sem necessidade.
2. Preservar comportamento padrão.
3. Documentar qualquer nova opção.
4. Validar impacto em instalações portáteis.
5. Testar comportamento com múltiplas configurações.

---

# Melhorias futuras sugeridas

- validação automática de configuração;
- exportação/importação de perfil;
- documentação automática das opções;
- backup automático opcional;
- modo seguro para inicialização.
