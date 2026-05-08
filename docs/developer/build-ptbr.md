# Guia de Build e Ambiente de Desenvolvimento

## Objetivo

Este documento orienta como preparar o ambiente para estudar, compilar e manter o projeto **mpv.net**.

> Observação: este guia é inicial e deve ser validado em uma máquina Windows com o ambiente de desenvolvimento completo.

---

# Requisitos principais

Com base na documentação atual do projeto, o mpv.net requer:

- Windows 10 ou superior;
- .NET Desktop Runtime 10.0 para execução;
- ambiente de desenvolvimento .NET compatível para compilação;
- dependências relacionadas ao mpv/libmpv conforme empacotamento do projeto.

Para desenvolvimento, recomenda-se:

- Windows 10 ou Windows 11;
- Visual Studio com workload de desenvolvimento desktop .NET;
- SDK do .NET correspondente ao projeto;
- Git;
- acesso ao repositório no GitHub.

---

# Clonando o fork

```bash
git clone https://github.com/WandersondeSouza/mpv.net.git
cd mpv.net
```

---

# Abrindo no Visual Studio

1. Abra o Visual Studio.
2. Selecione **Open a project or solution**.
3. Abra o arquivo de solução do projeto, caso exista.
4. Restaure os pacotes NuGet.
5. Compile em modo Debug.

---

# Abrindo no VS Code

O VS Code pode ser usado para análise, documentação e edição de código.

Para build/debug completo, o Visual Studio tende a ser mais adequado quando o projeto envolve aplicação desktop Windows.

Extensões recomendadas:

- C# Dev Kit;
- GitHub Pull Requests;
- Markdown All in One;
- GitLens.

---

# Build via terminal

Caso o projeto possua solução ou arquivos `.csproj`, o build poderá seguir o padrão:

```bash
dotnet restore
dotnet build
```

Se houver múltiplos projetos, informe explicitamente o caminho da solução ou projeto:

```bash
dotnet build caminho/do/projeto.csproj
```

---

# Execução em debug

A execução deve ser feita preferencialmente pelo Visual Studio até que o fluxo de build esteja completamente documentado.

Pontos a validar:

- se o executável encontra as dependências do mpv/libmpv;
- se os arquivos de configuração são criados/carregados corretamente;
- se a aplicação abre a janela principal;
- se reprodução de mídia local funciona;
- se fullscreen, menu de contexto e atalhos funcionam.

---

# Possíveis problemas comuns

## Runtime .NET ausente

Instale o runtime ou SDK exigido pelo projeto.

## Dependência do mpv/libmpv ausente

Verifique se as bibliotecas nativas necessárias estão no local esperado.

## Build falha por versão de SDK

Confira o `TargetFramework` nos arquivos `.csproj`.

## Aplicação compila mas não executa

Validar:

- arquitetura x64/x86;
- arquivos nativos;
- diretório de execução;
- permissões do Windows;
- dependências empacotadas.

---

# Checklist de validação manual

Após compilar, validar:

- abrir aplicação;
- abrir arquivo de vídeo;
- abrir arquivo de áudio;
- abrir imagem;
- testar play/pause;
- testar fullscreen;
- testar menu de contexto;
- abrir pasta de configuração;
- alterar uma opção simples;
- fechar e abrir novamente;
- verificar se configuração persistiu.

---

# Pendências deste guia

Este documento ainda deve ser enriquecido com:

- nome real da solução;
- nomes reais dos projetos `.csproj`;
- comando exato de build;
- configuração de release;
- fluxo de empacotamento;
- dependências nativas;
- instruções para gerar instalador/portable.
