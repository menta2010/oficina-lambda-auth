# Contrato de schema com o oficina-app

A Lambda lê `Clientes` e `Usuarios` e o job atualiza `Clientes.Ativo`, mas quem cria e versiona essas tabelas são as **migrations do `oficina-app`**. O arquivo `schema-oficina.sql` é uma cópia do schema gerado a partir delas; os testes de integração (`IntegracaoBancoTests`) sobem um PostgreSQL real, aplicam esse schema e executam as consultas da Lambda contra ele.

Se uma migration do app mudar `Clientes` ou `Usuarios`, atualize o contrato e rode os testes:

```bash
dotnet ef migrations script --idempotent \
  --project ../oficina-app/src/Oficina.Infrastructure \
  --startup-project ../oficina-app/src/Oficina.Infrastructure \
  -o tests/Oficina.Auth.Lambda.Tests/Contrato/schema-oficina.sql
dotnet test
```

Se uma coluna usada pela Lambda for renomeada ou removida, os testes quebram antes do deploy.
