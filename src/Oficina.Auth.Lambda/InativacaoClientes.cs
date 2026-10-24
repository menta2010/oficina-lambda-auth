using System.Diagnostics;
using Amazon.Lambda.Core;
using Npgsql;

namespace Oficina.Auth.Lambda;

/// <summary>
/// Politica de inativacao de clientes (ver ADR-005), executada por agendamento do EventBridge (diario).
/// Inativa clientes ativos que nao abrem OS ha N meses (padrao 24) e nao tem nenhuma OS em andamento.
/// Efeitos: o cliente deixa de conseguir se autenticar pelo CPF; ao abrir uma nova OS na loja ele
/// e reativado automaticamente pela API.
/// </summary>
public class InativacaoClientesFunction
{
    // O cliente e inativado se: esta ativo, foi criado antes do corte, e nao tem OS recente nem OS em aberto
    internal const string Sql = """
        UPDATE "Clientes" c
        SET "Ativo" = FALSE, "InativadoEm" = now()
        WHERE c."Ativo"
          AND c."CriadoEm" < @corte
          AND NOT EXISTS (
              SELECT 1 FROM "OrdensServico" o
              WHERE o."ClienteId" = c."Id"
                AND (o."CriadaEm" >= @corte OR NOT o."Encerrada"))
        """;

    private static NpgsqlDataSource? _dataSource;

    public async Task<object> FunctionHandler(object evento, ILambdaContext context)
    {
        var correlationId = context.AwsRequestId;
        var meses = int.TryParse(Environment.GetEnvironmentVariable("MESES_INATIVIDADE"), out var m) ? m : 24;
        var corte = DateTime.UtcNow.AddMonths(-meses);
        var cronometro = Stopwatch.StartNew();

        try
        {
            _dataSource ??= NpgsqlDataSource.Create((await Configuracao.CarregarAsync(exigirJwt: false)).ConnectionString);

            await using var cmd = _dataSource.CreateCommand(Sql);
            cmd.Parameters.AddWithValue("corte", corte);
            var inativados = await cmd.ExecuteNonQueryAsync();

            LogJson.Info("inativacao_clientes", correlationId, new
            {
                evento = "clientes_inativados",
                inativados,
                mesesInatividade = meses,
                corte,
                duracaoMs = cronometro.ElapsedMilliseconds
            });
            return new { inativados, corte };
        }
        catch (Exception ex)
        {
            LogJson.Erro("falha_inativacao_clientes", correlationId, ex);
            throw; // falha visivel no CloudWatch/New Relic e retentativa do EventBridge
        }
    }
}
