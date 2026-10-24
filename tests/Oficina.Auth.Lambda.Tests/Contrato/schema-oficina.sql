CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "Clientes" (
        "Id" uuid NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "Documento" character varying(14) NOT NULL,
        "Email" character varying(200),
        "Telefone" character varying(40),
        "Ativo" boolean NOT NULL,
        "CriadoEm" timestamp with time zone NOT NULL,
        "InativadoEm" timestamp with time zone,
        CONSTRAINT "PK_Clientes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "Lojas" (
        "Id" uuid NOT NULL,
        "Nome" character varying(120) NOT NULL,
        "Cidade" character varying(120) NOT NULL,
        "Ativa" boolean NOT NULL,
        CONSTRAINT "PK_Lojas" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "PecasInsumos" (
        "Id" uuid NOT NULL,
        "Nome" character varying(160) NOT NULL,
        "ValorUnitario" numeric(18,2) NOT NULL,
        CONSTRAINT "PK_PecasInsumos" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "Servicos" (
        "Id" uuid NOT NULL,
        "Nome" character varying(160) NOT NULL,
        "Descricao" character varying(500),
        "ValorBase" numeric(18,2) NOT NULL,
        "TempoEstimado" interval NOT NULL,
        CONSTRAINT "PK_Servicos" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "Veiculos" (
        "Id" uuid NOT NULL,
        "ClienteId" uuid NOT NULL,
        "Placa" character varying(7) NOT NULL,
        "Marca" character varying(80) NOT NULL,
        "Modelo" character varying(120) NOT NULL,
        "Ano" integer NOT NULL,
        CONSTRAINT "PK_Veiculos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Veiculos_Clientes_ClienteId" FOREIGN KEY ("ClienteId") REFERENCES "Clientes" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "Usuarios" (
        "Id" uuid NOT NULL,
        "Username" character varying(80) NOT NULL,
        "PasswordHash" character varying(300) NOT NULL,
        "Role" character varying(40) NOT NULL,
        "LojaId" uuid,
        CONSTRAINT "PK_Usuarios" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Usuarios_Lojas_LojaId" FOREIGN KEY ("LojaId") REFERENCES "Lojas" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "EstoquesLoja" (
        "Id" uuid NOT NULL,
        "LojaId" uuid NOT NULL,
        "PecaInsumoId" uuid NOT NULL,
        "Quantidade" integer NOT NULL,
        CONSTRAINT "PK_EstoquesLoja" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_EstoquesLoja_Quantidade" CHECK ("Quantidade" >= 0),
        CONSTRAINT "FK_EstoquesLoja_Lojas_LojaId" FOREIGN KEY ("LojaId") REFERENCES "Lojas" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EstoquesLoja_PecasInsumos_PecaInsumoId" FOREIGN KEY ("PecaInsumoId") REFERENCES "PecasInsumos" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "OrdensServico" (
        "Id" uuid NOT NULL,
        "LojaId" uuid NOT NULL,
        "ClienteId" uuid NOT NULL,
        "VeiculoId" uuid NOT NULL,
        "Status" character varying(30) NOT NULL,
        "Orcamento_ValorServicos" numeric(18,2),
        "Orcamento_ValorPecas" numeric(18,2),
        "Orcamento_ValorTotal" numeric(18,2),
        "Orcamento_PrevisaoEntrega" timestamp with time zone,
        "Orcamento_GeradoEm" timestamp with time zone,
        "Encerrada" boolean NOT NULL,
        "CriadaEm" timestamp with time zone NOT NULL,
        "DiagnosticoIniciadoEm" timestamp with time zone,
        "ExecucaoIniciadaEm" timestamp with time zone,
        "ExecucaoFinalizadaEm" timestamp with time zone,
        "EntregueEm" timestamp with time zone,
        "CanceladaEm" timestamp with time zone,
        "EncerradaEm" timestamp with time zone,
        CONSTRAINT "PK_OrdensServico" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OrdensServico_Clientes_ClienteId" FOREIGN KEY ("ClienteId") REFERENCES "Clientes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_OrdensServico_Lojas_LojaId" FOREIGN KEY ("LojaId") REFERENCES "Lojas" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_OrdensServico_Veiculos_VeiculoId" FOREIGN KEY ("VeiculoId") REFERENCES "Veiculos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "HistoricoStatusOS" (
        "Id" uuid NOT NULL,
        "Status" character varying(30) NOT NULL,
        "Inicio" timestamp with time zone NOT NULL,
        "Fim" timestamp with time zone,
        "OrdemServicoId" uuid,
        CONSTRAINT "PK_HistoricoStatusOS" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_HistoricoStatusOS_OrdensServico_OrdemServicoId" FOREIGN KEY ("OrdemServicoId") REFERENCES "OrdensServico" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "ItensPeca" (
        "Id" uuid NOT NULL,
        "PecaId" uuid NOT NULL,
        "Quantidade" integer NOT NULL,
        "ValorUnitario" numeric(18,2) NOT NULL,
        "Utilizado" boolean NOT NULL,
        "OrdemServicoId" uuid,
        CONSTRAINT "PK_ItensPeca" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ItensPeca_OrdensServico_OrdemServicoId" FOREIGN KEY ("OrdemServicoId") REFERENCES "OrdensServico" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE TABLE "ItensServico" (
        "Id" uuid NOT NULL,
        "ServicoId" uuid NOT NULL,
        "Descricao" character varying(300) NOT NULL,
        "Valor" numeric(18,2) NOT NULL,
        "TempoEstimado" interval NOT NULL,
        "Executado" boolean NOT NULL,
        "OrdemServicoId" uuid,
        CONSTRAINT "PK_ItensServico" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ItensServico_OrdensServico_OrdemServicoId" FOREIGN KEY ("OrdemServicoId") REFERENCES "OrdensServico" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_Clientes_Ativo" ON "Clientes" ("Ativo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE UNIQUE INDEX "IX_Clientes_Documento" ON "Clientes" ("Documento");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE UNIQUE INDEX "IX_EstoquesLoja_LojaId_PecaInsumoId" ON "EstoquesLoja" ("LojaId", "PecaInsumoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_EstoquesLoja_PecaInsumoId" ON "EstoquesLoja" ("PecaInsumoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_HistoricoStatusOS_OrdemServicoId_Inicio" ON "HistoricoStatusOS" ("OrdemServicoId", "Inicio");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_ItensPeca_OrdemServicoId" ON "ItensPeca" ("OrdemServicoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_ItensServico_OrdemServicoId" ON "ItensServico" ("OrdemServicoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE UNIQUE INDEX "IX_Lojas_Nome" ON "Lojas" ("Nome");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_OrdensServico_ClienteId" ON "OrdensServico" ("ClienteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_OrdensServico_Listagem" ON "OrdensServico" ("Encerrada", "LojaId", "Status", "CriadaEm");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_OrdensServico_LojaId" ON "OrdensServico" ("LojaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_OrdensServico_VeiculoId" ON "OrdensServico" ("VeiculoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_Usuarios_LojaId" ON "Usuarios" ("LojaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE UNIQUE INDEX "IX_Usuarios_Username" ON "Usuarios" ("Username");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE INDEX "IX_Veiculos_ClienteId" ON "Veiculos" ("ClienteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    CREATE UNIQUE INDEX "IX_Veiculos_Placa" ON "Veiculos" ("Placa");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024151728_Fase3ModeloInicial') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024151728_Fase3ModeloInicial', '8.0.6');
    END IF;
END $EF$;
COMMIT;

