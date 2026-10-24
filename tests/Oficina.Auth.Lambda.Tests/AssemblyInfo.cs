// LogJson.Saida e estatico (a Lambda escreve no stdout); os testes que capturam logs
// nao podem rodar em paralelo com outros que tambem invocam a Function.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
