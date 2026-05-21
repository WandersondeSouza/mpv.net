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

Em validação de 2026-05-21, um arquivo MP4 com caminho de 325 caracteres chegou ao comando `loadfile`, mas libmpv falhou com `Invalid argument` quando recebeu o caminho Windows normal. O mesmo arquivo abriu corretamente quando o caminho foi entregue no formato estendido `\\?\C:\...`.

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

Depois da validação prática, o carregamento central em `Player.ConvertFilePath` passou a converter automaticamente arquivos locais existentes com caminho de 260 caracteres ou mais para o formato estendido do Windows antes de chamar `loadfile`.

## Validação executada

Validado em 2026-05-21:

1. arquivo MP4 real com caminho de 325 caracteres;
2. abertura direta pelo terminal com caminho normal, convertida internamente para `\\?\`;
3. abertura direta com caminho `\\?\`;
4. repasse para instância única: a segunda instância encerrou após enviar o caminho e a primeira instância abriu o arquivo com `\\?\`.

## Testes manuais recomendados

Ainda falta validar com um arquivo real em caminho acima de 260 caracteres nos fluxos que dependem do Windows Shell:

1. abrir pelo Explorer;
2. abrir por associação de arquivo;
3. abrir via menu `Open Files...`.

Se a falha continuar apenas em um desses caminhos, a próxima investigação deve comparar o valor que chega a `Player.LoadFiles` com o valor aceito diretamente pelo `mpv.exe`.
