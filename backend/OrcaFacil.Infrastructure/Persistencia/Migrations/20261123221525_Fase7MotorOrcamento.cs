using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Fase7MotorOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Produto_PrecoReferencia",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_ItensOrcamento_OpcaoOrcamentoId",
                table: "ItensOrcamento");

            migrationBuilder.DropColumn(
                name: "OrigemPrecoReferencia",
                table: "Produtos");

            migrationBuilder.RenameColumn(
                name: "PrecoReferencia",
                table: "Produtos",
                newName: "PrecoVenda");

            migrationBuilder.AddColumn<string>(
                name: "GrupoEquivalencia",
                table: "Produtos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modo",
                table: "Orcamentos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Comparativo"); // ajuste manual: registros que já existem começam no modo Comparativo ("" quebraria a leitura do enum)

            migrationBuilder.AddColumn<Guid>(
                name: "ItemListaImportadaId",
                table: "ItensOrcamento",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacao",
                table: "ItensOrcamento",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Ordem",
                table: "ItensOrcamento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PrecoPendente",
                table: "ItensOrcamento",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModoOrcamentoPadrao",
                table: "Empresas",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Comparativo"); // ajuste manual: registros que já existem começam no modo Comparativo ("" quebraria a leitura do enum)

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_EmpresaId_GrupoEquivalencia",
                table: "Produtos",
                columns: new[] { "EmpresaId", "GrupoEquivalencia" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Produto_PrecoVenda",
                table: "Produtos",
                sql: "\"PrecoVenda\" IS NULL OR \"PrecoVenda\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ItensOrcamento_OpcaoOrcamentoId_Ordem",
                table: "ItensOrcamento",
                columns: new[] { "OpcaoOrcamentoId", "Ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Produtos_EmpresaId_GrupoEquivalencia",
                table: "Produtos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Produto_PrecoVenda",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_ItensOrcamento_OpcaoOrcamentoId_Ordem",
                table: "ItensOrcamento");

            migrationBuilder.DropColumn(
                name: "GrupoEquivalencia",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Modo",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ItemListaImportadaId",
                table: "ItensOrcamento");

            migrationBuilder.DropColumn(
                name: "Observacao",
                table: "ItensOrcamento");

            migrationBuilder.DropColumn(
                name: "Ordem",
                table: "ItensOrcamento");

            migrationBuilder.DropColumn(
                name: "PrecoPendente",
                table: "ItensOrcamento");

            migrationBuilder.DropColumn(
                name: "ModoOrcamentoPadrao",
                table: "Empresas");

            migrationBuilder.RenameColumn(
                name: "PrecoVenda",
                table: "Produtos",
                newName: "PrecoReferencia");

            migrationBuilder.AddColumn<string>(
                name: "OrigemPrecoReferencia",
                table: "Produtos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Produto_PrecoReferencia",
                table: "Produtos",
                sql: "\"PrecoReferencia\" IS NULL OR \"PrecoReferencia\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ItensOrcamento_OpcaoOrcamentoId",
                table: "ItensOrcamento",
                column: "OpcaoOrcamentoId");
        }
    }
}
