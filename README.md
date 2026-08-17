# CoreSearch

Um utilitário simples e rápido para buscar textos dentro de arquivos no Windows.

Construído em **C# / .NET 8 (WPF)** com foco em velocidade, baixo consumo de memória e uma interface direta ao ponto.

---

## ✨ Recursos

- **Busca rápida em arquivos**: Processamento multithread que aproveita todos os núcleos do processador.
- **Filtros por extensão**: Suporte a múltiplos padrões (ex: `*.cs;*.xaml`, `*.txt`, `*test*.json`).
- **Opções de pesquisa**:
  - Diferenciar maiúsculas/minúsculas (*Case sensitive*).
  - Casamento de palavra inteira.
  - Busca recursiva em subpastas.
- **Navegação rápida**:
  - Pressione `Enter` para iniciar a busca.
  - Dê duplo clique no resultado para abrir o arquivo ou use o menu de contexto para revelar no Explorer.
- **Leitura segura**: Ignora arquivos binários e pastas protegidas do sistema sem travar ou interromper a pesquisa.

---

## 🚀 Como Executar

### Pré-requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) instalado no Windows.

### Rodando o projeto
No terminal, na pasta raiz do repositório:

```bash
dotnet build
dotnet run
```

---

## 🛠️ Tecnologias

- **C# 12 / .NET 8**
- **WPF (Windows Presentation Foundation)**
- **System.Threading.Channels** para streaming e paralelismo de busca
