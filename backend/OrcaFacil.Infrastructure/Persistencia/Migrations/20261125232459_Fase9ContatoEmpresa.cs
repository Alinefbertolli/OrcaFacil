using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Fase9ContatoEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Empresas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoBairro",
                table: "Empresas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoCep",
                table: "Empresas",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoCidade",
                table: "Empresas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoComplemento",
                table: "Empresas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoLogradouro",
                table: "Empresas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoNumero",
                table: "Empresas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnderecoUf",
                table: "Empresas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefone",
                table: "Empresas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EnderecoBairro",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EnderecoCep",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EnderecoCidade",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EnderecoComplemento",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EnderecoLogradouro",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EnderecoNumero",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "EnderecoUf",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Telefone",
                table: "Empresas");
        }
    }
}
