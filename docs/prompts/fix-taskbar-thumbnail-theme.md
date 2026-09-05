# Prompt para Codex — corrigir tema dos botões da Taskbar Thumbnail Toolbar

TAREFA: corrigir os ícones dos controles da miniatura da barra de tarefas
(Taskbar Thumbnail Toolbar) do projeto mpv.net para respeitar automaticamente
o tema claro/escuro do Windows.

Repositório:
WandersondeSouza/mpv.net

IMPORTANTE:
Antes de alterar qualquer código, analise a implementação existente na branch
atual e preserve integralmente o funcionamento já existente do SMTC e da
Taskbar Thumbnail Toolbar.

## CONTEXTO DO PROBLEMA

Recentemente foram implementados os controles de mídia do Windows/SMTC e também
uma toolbar na miniatura da janela exibida ao passar o mouse sobre o ícone do
mpv.net na barra de tarefas.

A toolbar contém atualmente:

- Previous
- Play/Pause
- Next
- Donation

Esses controles funcionam corretamente.

O problema é exclusivamente visual.

Arquivo principal envolvido:

`src/MpvNet.Windows/Native/Taskbar.cs`

A implementação atual gera programaticamente os ícones da toolbar usando
System.Drawing e CreateThumbnailIcon(...).

Atualmente existe um pincel fixo semelhante a:

```csharp
new SolidBrush(Color.White)
```

Isso faz com que todos os glyphs sejam sempre brancos.

Quando o Windows está usando tema escuro isso funciona corretamente.

Quando o Windows está usando o tema claro/padrão, a superfície da miniatura
também fica clara e os ícones brancos praticamente desaparecem.

O clique continua funcionando, portanto NÃO estamos diante de um problema de
SMTC, COM, ThumbBarAddButtons, ThumbBarUpdateButtons ou processamento de
WM_COMMAND.

O problema é apenas contraste/tema dos ícones.

## OBJETIVO

Fazer com que os ícones da Taskbar Thumbnail Toolbar tenham automaticamente
contraste adequado com o tema atual do Windows.

Resultado esperado:

Windows Dark:
- utilizar glyphs claros apropriados.

Windows Light:
- utilizar glyphs escuros apropriados.

A implementação deve funcionar de maneira semelhante aos controles nativos do
Windows.

## REQUISITOS IMPORTANTES

### 1. Não trocar apenas branco por preto

NÃO substituir simplesmente Color.White por Color.Black.

A solução precisa ser adaptativa ao tema do Windows.

### 2. Detectar corretamente o tema do Windows

Estudar como detectar corretamente o tema do Windows usado para aplicativos.

Preferir APIs/configurações oficiais ou padrões amplamente utilizados pelo
Windows.

Investigar, entre outros, conforme apropriado:

- AppsUseLightTheme
- SystemUsesLightTheme
- Microsoft.Win32.Registry
- Windows.UI.ViewManagement.UISettings
- APIs Win32/WinRT disponíveis no target atual

Escolher a alternativa tecnicamente mais adequada para o projeto.

O projeto atualmente trabalha com:

- .NET 10
- net10.0-windows10.0.19041.0

Não adicionar dependências pesadas apenas para detectar o tema.

### 3. Centralizar a lógica de tema

A lógica de tema NÃO deve ficar espalhada pelo projeto.

Criar uma abstração simples ou método claramente nomeado, por exemplo:

`IsWindowsLightTheme()`

ou equivalente.

Se já existir infraestrutura de tema no projeto, reutilizá-la.

ANTES de criar código novo, pesquisar o repositório por:

- theme
- dark
- light
- AppsUseLightTheme
- UISettings
- SystemTheme
- WindowsTheme
- WM_SETTINGCHANGE
- WM_THEMECHANGED

Evitar duplicação.

### 4. Alterar CreateThumbnailIcon(...)

O método deve escolher a cor de glyph de acordo com o tema.

Conceitualmente:

Light Windows theme
→ glyph escuro

Dark Windows theme
→ glyph claro

Não assumir necessariamente preto puro e branco puro caso exista uma cor
mais apropriada aos controles nativos do Windows.

Priorizar contraste e aparência coerente com Windows 11.

### 5. Cache dos ícones

Atualmente existe:

```csharp
Dictionary<TaskbarThumbnailIcon, Icon> _thumbnailIcons
```

Esse cache precisa ser analisado.

Se os ícones dependem do tema, não é correto reutilizar indefinidamente o mesmo
Icon depois que o tema muda.

Implementar uma estratégia correta, por exemplo:

- incluir o tema na chave do cache

OU

- invalidar e recriar o cache quando o tema mudar.

Evitar vazamento de handles GDI/HICON.

Todos os Icon descartados devem continuar recebendo Dispose.

Preservar o DestroyIcon existente utilizado após Bitmap.GetHicon().

### 6. Troca de tema com o programa aberto

Verificar se é possível suportar:

- Windows Dark → Light
- Windows Light → Dark

sem precisar reiniciar o mpv.net.

O mpv.net deve detectar a alteração do tema do Windows e atualizar/recriar os
ícones da Taskbar Thumbnail Toolbar.

Investigar as mensagens/eventos Windows adequados ao contexto atual do projeto,
por exemplo:

- WM_SETTINGCHANGE
- WM_THEMECHANGED

ou outra solução mais correta.

Se a alteração dinâmica puder ser feita de forma simples e segura, implementar.

Quando o tema mudar:

1. detectar novo tema;
2. invalidar/liberar os ícones antigos;
3. criar os novos ícones;
4. chamar ThumbBarUpdateButtons com os mesmos botões e estados atuais.

NÃO perder:

- estado de Previous;
- estado Play/Pause;
- estado Next;
- Donation;
- enabled/disabled;
- tooltips.

### 7. Play/Pause

Preservar integralmente o comportamento atual.

O projeto já teve ajustes recentes específicos no Play/Pause.

NÃO modificar a lógica de:

```csharp
Player.Command("cycle pause")
```

ou qualquer fluxo atual, salvo se for estritamente necessário para a atualização
visual.

Esta tarefa NÃO é uma refatoração funcional do SMTC.

### 8. SMTC

NÃO modificar desnecessariamente:

`src/MpvNet.Windows/Services/MediaTransport/WindowsSystemMediaTransportService.cs`

A toolbar da miniatura da barra de tarefas é uma integração Win32 diferente do
SMTC.

Não misturar as duas responsabilidades.

### 9. Fullscreen

Preservar o comportamento atual relacionado a fullscreen.

A toolbar/SMTC já possuem regras próprias para aparecer/desaparecer ou suspender
estado.

A correção de tema não pode introduzir regressão nesse fluxo.

### 10. Ícones existentes

Preservar exatamente os glyphs existentes:

- Previous
- Play
- Pause
- Next
- Donation

Não redesenhar desnecessariamente a geometria.

A alteração deve ser prioritariamente:

geometria existente + cor adaptativa ao tema.

### 11. Escala / DPI

Verificar se CreateThumbnailIcon continua correto em:

- 100%
- 125%
- 150%
- 175%
- 200%

Não introduzir valores que quebrem High DPI.

Preservar a lógica baseada em:

```csharp
SystemInformation.IconSize.Width
```

caso continue adequada.

### 12. Testes

Criar testes unitários onde for tecnicamente possível.

Separar a decisão de cor/tema da criação GDI se isso permitir testes simples.

Testar pelo menos:

Theme Light
→ glyph escuro

Theme Dark
→ glyph claro

Mudança de estado:
- Dark → Light
- Light → Dark

Cache:
- não devolver ícone do tema anterior quando o tema muda.

Não tentar automatizar teste visual da shell do Windows se isso tornar a suíte
frágil.

### 13. Teste manual obrigatório

#### CENÁRIO A

1. Windows 11 em modo Light.
2. iniciar mpv.net.
3. abrir vídeo.
4. passar mouse sobre ícone do mpv.net na taskbar.
5. confirmar que aparecem claramente:
   - Previous
   - Play/Pause
   - Next
   - Donation.

#### CENÁRIO B

1. mudar Windows para Dark.
2. repetir.
3. confirmar contraste adequado.

#### CENÁRIO C

Se a alteração dinâmica tiver sido implementada:

1. abrir mpv.net no modo Light;
2. manter vídeo reproduzindo;
3. mudar Windows para Dark;
4. abrir novamente a miniatura;
5. confirmar atualização dos glyphs sem reiniciar.

Repetir Dark → Light.

#### CENÁRIO D

Validar Play → Pause → Play.

O glyph precisa acompanhar corretamente o estado do player depois da mudança
de tema.

#### CENÁRIO E

Testar Previous e Next com playlist contendo mais de um item.

#### CENÁRIO F

Testar Donation.

### 14. Qualidade

Não deixar:

- handles GDI vazando;
- Icons sem Dispose;
- eventos de sistema inscritos depois do Dispose;
- COM objects sem liberação;
- Bitmap/Graphics/Brush sem Dispose;
- duplicação de código;
- tratamento silencioso de exceções relevantes.

Preservar o padrão de logging atual do projeto.

### 15. Documentação

Atualizar, somente se necessário:

`docs/developer/windows-ui.md`

Explicar brevemente que os glyphs da Taskbar Thumbnail Toolbar são adaptados ao
tema claro/escuro do Windows.

Não alterar documentação não relacionada.

### 16. Build e testes

Executar:

```bash
dotnet restore
dotnet build
dotnet test
```

Corrigir qualquer regressão causada pela alteração.

Não mascarar testes existentes.

### 17. Versionamento do trabalho

Criar uma branch nova a partir da branch atualmente correta, com nome semelhante:

`fix/taskbar-thumbnail-theme`

Antes de modificar:

```bash
git status
git branch --show-current
git log --oneline -10
```

Confirmar que não existem alterações locais do usuário que possam ser perdidas.

### 18. Commits

Fazer commits pequenos e objetivos.

Sugestão:

commit 1:
`fix(taskbar): adapt thumbnail icons to Windows theme`

commit 2:
`test(taskbar): cover thumbnail icon theme selection`

commit 3:
`docs(taskbar): document adaptive thumbnail controls`

Não criar commits vazios só para seguir essa divisão.

### 19. Push

Ao final:

- executar todos os testes;
- confirmar git status limpo;
- fazer push da branch para origin.

Não fazer merge em main.

### 20. Relatório final

Apresentar:

- causa raiz encontrada;
- arquivos alterados;
- mecanismo usado para detectar o tema;
- como foi tratado o cache dos Icons;
- como foi tratada a mudança dinâmica de tema;
- resultado de dotnet build;
- resultado de dotnet test;
- commits criados;
- branch criada;
- confirmação do push;
- qualquer limitação restante.

## RESTRIÇÃO PRINCIPAL

Esta correção deve permanecer focada exclusivamente em tornar os botões da
Taskbar Thumbnail Toolbar visíveis e coerentes nos temas Light e Dark do
Windows.

Não aproveitar a tarefa para refatorações gerais do player.
Não alterar comportamento funcional do SMTC.
Não alterar comandos de reprodução que já estão funcionando.
