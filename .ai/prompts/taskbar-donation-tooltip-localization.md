# Codex Prompt — Localizar tooltip do botão de doação da toolbar da barra de tarefas

## Contexto

No projeto `mpv.net`, a toolbar exibida na miniatura da janela na barra de tarefas do Windows possui os botões Anterior, Play/Pause, Próximo e Doação.

O botão de doação está funcionando corretamente e abre `App.DonationUrl`, porém o tooltip aparece sempre como `Donation`, independentemente do idioma selecionado no aplicativo.

A implementação atual está em:

- `src/MpvNet.Windows/WinForms/MainForm.Taskbar.cs`

Trecho atual:

```csharp
new TaskbarThumbnailButton(
    TaskbarThumbnailButtonId.Donation,
    TaskbarThumbnailIcon.Donation,
    _("Donation"),
    true),
```

Os demais botões usam o mesmo mecanismo de tradução, por exemplo:

```csharp
_("Previous File")
_("Play/Pause")
_("Next File")
```

O problema esperado é que a chave `Donation` não esteja corretamente presente nos catálogos/arquivos de tradução usados pelo projeto, fazendo com que o fallback permaneça em inglês.

## Objetivo

Corrigir o tooltip do botão de doação para que seja traduzido de acordo com o idioma atual da interface e, ao mesmo tempo, trocar o texto exibido ao usuário de `Donation` para uma expressão mais amigável equivalente a **"Help the player"**.

Em português do Brasil, o tooltip deve aparecer como:

**Ajude o player**

A alteração deve utilizar exatamente a infraestrutura de localização já existente no mpv.net, sem criar um sistema de tradução paralelo.

## Requisitos obrigatórios

### 1. Analisar primeiro o sistema atual de localização

Antes de alterar qualquer arquivo:

1. Localize a implementação de `_()` usada em `MainForm.Taskbar.cs`.
2. Identifique onde estão armazenadas as traduções do projeto.
3. Descubra como as chaves dos tooltips dos demais botões da toolbar são traduzidas.
4. Confirme por que `_("Donation")` atualmente retorna `Donation` em todos os idiomas.
5. Verifique os mecanismos atuais de fallback de idioma, incluindo variantes como `pt-BR`, `pt-PT`, `en`, `es`, etc.

Não presuma a estrutura dos arquivos de idioma; valide no código antes de modificar.

### 2. Alterar apenas o texto visível

Trocar a chave/texto visível do tooltip de:

```csharp
_("Donation")
```

para algo equivalente a:

```csharp
_("Help the player")
```

ou para a forma tecnicamente correta exigida pelo sistema de localização já existente no projeto.

Não alterar desnecessariamente:

- `TaskbarThumbnailButtonId.Donation`
- `TaskbarThumbnailIcon.Donation`
- `App.DonationUrl`
- tratamento do clique do botão
- desenho/ícone do coração

O botão deve continuar abrindo exatamente o mesmo endereço de doação existente hoje.

### 3. Adicionar traduções em todos os idiomas suportados pelo projeto

Adicionar a nova chave seguindo os padrões existentes.

Referências mínimas esperadas:

- English: `Help the player`
- Português (Brasil): `Ajude o player`
- Português (Portugal): `Apoiar o player`
- Español: `Ayuda al reproductor`
- Français: `Soutenir le lecteur`
- Deutsch: `Player unterstützen`
- Italiano: `Supporta il player`

Essas traduções são apenas referências iniciais. O Codex deve:

1. identificar todos os idiomas efetivamente existentes no repositório;
2. adicionar uma tradução apropriada para cada idioma;
3. manter terminologia, capitalização, encoding e formato coerentes com os demais textos do projeto;
4. evitar traduções automáticas inadequadas quando já existir no projeto um termo equivalente para player/reprodutor/apoio/doação.

### 4. Respeitar os fallbacks existentes

Validar que:

- `pt-BR` utiliza a tradução brasileira;
- `pt-PT` utiliza a tradução portuguesa;
- variantes regionais sem tradução própria seguem o fallback já adotado pelo projeto;
- idiomas desconhecidos continuam usando o fallback padrão existente;
- ausência eventual de uma tradução nunca causa exceção.

Não modificar a estratégia global de fallback salvo se for identificado um bug diretamente relacionado e a correção for pequena, segura e comprovadamente necessária.

### 5. Verificar outros usos de `Donation`

Pesquisar no repositório por:

```text
Donation
_("Donation")
```

Identificar se a mesma chave é utilizada em menus, janelas, documentação ou outros controles.

Não realizar alterações globais cegas. Diferenciar:

- identificadores internos, que podem continuar com o nome `Donation`;
- textos visíveis ao usuário, que podem precisar de localização;
- documentação técnica, que só deve ser ajustada se ficar incorreta após a mudança.

### 6. Testes

Criar ou atualizar testes quando a arquitetura atual permitir.

Validar pelo menos:

1. compilação da solução;
2. ausência de erros nos arquivos de localização;
3. carregamento correto da nova chave;
4. `pt-BR` => `Ajude o player`;
5. inglês => `Help the player`;
6. pelo menos uma segunda língua além de inglês e português;
7. fallback quando uma cultura específica não possui entrada própria;
8. o clique no botão continua abrindo `App.DonationUrl`;
9. Previous, Play/Pause e Next permanecem sem regressões.

Se os tooltips Win32 não forem facilmente testáveis por teste automatizado, documentar o teste manual necessário.

### 7. Teste manual no Windows

Após compilar:

1. iniciar o mpv.net;
2. carregar uma mídia;
3. passar o mouse sobre a miniatura do programa na barra de tarefas;
4. posicionar o cursor sobre o botão de coração/doação;
5. confirmar o tooltip no idioma atual;
6. mudar o idioma do aplicativo;
7. reiniciar a aplicação se o mecanismo atual de idioma exigir;
8. confirmar que o tooltip acompanha o idioma selecionado;
9. clicar no botão e confirmar que a página de doação continua sendo aberta normalmente.

Testar pelo menos:

- Português (Brasil)
- English
- mais um idioma disponível no projeto

## Qualidade da alteração

A solução deve ser pequena, localizada e alinhada à arquitetura atual.

Evitar:

- strings hardcoded específicas por idioma dentro de `MainForm.Taskbar.cs`;
- `switch` por cultura no código da toolbar;
- duplicação da infraestrutura de localização;
- alteração dos IDs Win32 dos botões;
- alteração do comportamento do SMTC;
- alteração desnecessária de outras partes da interface.

## Documentação

Se existir documentação descrevendo explicitamente o botão como `Donation`/`Doação`, ajustar apenas quando necessário para refletir que o texto visível agora é equivalente a `Help the player` / `Ajude o player`.

Não alterar documentação sem necessidade.

## Git e execução

1. Trabalhar na branch atual, a menos que exista uma instrução explícita no repositório exigindo branch própria para a tarefa.
2. Antes de alterar, verificar `git status` e não sobrescrever mudanças não relacionadas.
3. Fazer a implementação em etapas pequenas e verificáveis.
4. Executar os testes relevantes e a compilação antes do commit.
5. Revisar o diff final procurando alterações acidentais.
6. Criar commit com mensagem clara, por exemplo:

```text
fix: localize taskbar donation tooltip
```

7. Fazer `push` para o repositório remoto na branch de trabalho.

## Critérios de aceite

A tarefa só está concluída quando:

- o tooltip não aparece mais permanentemente como `Donation`;
- em inglês aparece `Help the player`;
- em `pt-BR` aparece `Ajude o player`;
- os demais idiomas suportados possuem tradução coerente;
- o sistema existente de localização/fallback continua sendo utilizado;
- o botão continua abrindo `App.DonationUrl`;
- os demais botões da toolbar continuam funcionando;
- a solução compila;
- os testes relevantes passam;
- o diff final não contém alterações não relacionadas;
- commit e push foram concluídos.

## Entrega final do Codex

Ao concluir, informar de forma objetiva:

1. causa raiz encontrada;
2. arquivos modificados;
3. idiomas/chaves adicionados ou alterados;
4. testes executados e seus resultados;
5. validação do fallback;
6. confirmação de que `App.DonationUrl` e o comportamento do clique não foram alterados;
7. hash do commit;
8. branch que recebeu o push;
9. qualquer limitação de teste manual ainda existente.
