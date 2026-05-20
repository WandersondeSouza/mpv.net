# Caminhos longos no Windows

Este documento registra a investigação inicial sobre abertura de arquivos com caminhos longos no mpv.net.

## Estado atual

O executável Windows já declara suporte a caminhos longos no manifesto:

```text
src/MpvNet.Windows/app.manifest
```

com:

```text
longPathAware=true
```

Em teste local com .NET, `File.Exists`, `Path.GetFullPath` e `Directory.GetFiles` reconheceram um arquivo com caminho acima de 260 caracteres. Portanto, o problema não parece ser apenas uma limitação geral do .NET nesta máquina.

## Fluxos revisados

Os argumentos de linha de comando são processados em:

```text
src/MpvNet/CommandLine.cs
```

Quando já existe uma instância aberta, a nova chamada é repassada pela primeira instância em:

```text
src/MpvNet.Windows/Program.cs
```

A regra para reconhecer um argumento carregável foi centralizada para aceitar:

- URLs;
- `-` para entrada padrão;
- caminhos absolutos Windows com `C:\`;
- caminhos absolutos Windows com `C:/`;
- caminhos UNC ou estendidos, incluindo `\\?\`;
- caminhos relativos existentes.

## Testes manuais recomendados

Validar com um arquivo real em caminho acima de 260 caracteres:

1. abrir pelo terminal com `mpvnet.exe "<caminho longo>"`;
2. abrir pelo Explorer;
3. abrir por associação de arquivo;
4. abrir uma segunda vez com uma instância do mpv.net já em execução;
5. testar com caminho `C:\...`, `C:/...` e, se necessário, `\\?\C:\...`.

Se a falha continuar apenas em um desses caminhos, a próxima investigação deve comparar o valor que chega a `Player.LoadFiles` com o valor aceito diretamente pelo `mpv.exe`.
