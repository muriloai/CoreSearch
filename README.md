# Core Search

Uma ferramenta desktop leve e rápida para busca de texto em arquivos no Windows.

---

## Por que o Core Search?

Por padrão, a busca do Windows Explorer **não pesquisa conteúdo dentro de arquivos de código-fonte** (`.cs`, `.json`, `.ts`, `.py`, `.log`, etc.), limitando-se aos nomes dos arquivos ou exigindo a ativação de indexação pesada no sistema.

O **Core Search** resolve esta limitação:

- **Busca Real em Código-Fonte**: Pesquisa o conteúdo exato de qualquer arquivo de texto simples, independentemente da extensão.
- **Sem travamentos**: A leitura é feita via *stream* linha por linha (`StreamReader`), permitindo pesquisar em arquivos de múltiplos gigabytes sem estourar a memória RAM.
- **Resiliente a bloqueios**: Erros de permissão de pasta (`UnauthorizedAccessException`) e arquivos abertos em outros programas (`IOException`) são ignorados silenciosamente sem interromper o restante da busca.
- **Interface sempre responsiva**: A UI roda em threads separadas com suporte a cancelamento instantâneo.

---

## Funcionalidades

- **Busca Assíncrona**: Interface responsiva durante pesquisas em diretórios extensos.
- **Baixo Consumo de Memória**: Leitura sob demanda (stream) linha a linha, sem carregar arquivos inteiros na RAM.
- **Filtros Flexíveis**: Suporte a múltiplas extensões (`*.cs;*.txt;*.log`), busca por palavra inteira e diferenciação de maiúsculas/minúsculas.
- **Integração com o Explorer**: Na tabela de resultados o clique duplo em uma linha vai abrir o arquivo e ao clicar com botão direito do mouse abrirá o menu de contexto com opções para abrir o arquivo ou ir para sua pasta no Windows Explorer.

---

## Arquitetura

O projeto foi desenvolvido em **C# / .NET 8** e **WPF** seguindo os princípios de **Clean Architecture** e **MVVM**:

```
CoreSearch/
├── Models/         # Data contracts (SearchResult, SearchOptions)
├── Services/       # Lógica core de busca (ISearchEngine, SearchEngine)
├── ViewModels/     # Gestão de estado e comandos da UI (MainViewModel)
└── MainWindow.xaml # Interface gráfica WPF
```

### Principais Decisões de Design

- **Desacoplamento**: O motor de busca (`SearchEngine`) é desacoplado da interface gráfica e implementa `ISearchEngine`, facilitando testes unitários e extensões.
- **Fluxo Reativo**: Notificação de resultados em tempo real via `IProgress<SearchResult>`, atualizando a UI de forma segura sem travamentos.
- **Cancelamento Cooperativo**: Utilização de `CancellationToken` repassado diretamente para a leitura dos streams e travessia de pastas.

---

## Como Usar

### Download do Executável (.exe)
Você pode baixar a versão pronta na aba **[Releases](../../releases)** (não necessita de .NET instalado).

### Compilar a partir do código
Requer o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0):

```bash
# Executar o projeto
dotnet run

# Gerar executável único para distribuição
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -o ./publish
```