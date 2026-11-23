namespace OrcaFacil.Domain.Enums;

// Enumerações pequenas agrupadas num único arquivo para facilitar a leitura.
// Valores explícitos: são persistidos como texto, mas os números ficam estáveis para a API.

/// <summary>Ramo do varejista. Usado para sugestões (categorias, unidades), nunca para mudar regras.</summary>
public enum SegmentoEmpresa
{
    Outro = 0,
    Papelaria = 1,
    MaterialConstrucao = 2,
    Informatica = 3,
    Eletronicos = 4,
    Utilidades = 5,
    Alimentacao = 6,
    Farmacia = 7,
    Vestuario = 8,
    Autopecas = 9
}

public enum PerfilUsuario
{
    Operador = 1,
    Gerente = 2,
    Administrador = 3
}

public enum TipoPessoa
{
    Fisica = 1,
    Juridica = 2
}

public enum StatusOrcamento
{
    EmElaboracao = 1,
    AguardandoAprovacao = 2,
    Aprovado = 3,
    Reprovado = 4,
    Finalizado = 5
}

public enum TipoOpcaoOrcamento
{
    CustoBeneficio = 1,
    MarcasReconhecidas = 2,
    MenorPreco = 3,
    Manual = 4,
    /// <summary>Modo "orçamento único": os itens exatos que o cliente pediu, sem comparação.</summary>
    Unica = 5
}

/// <summary>
/// Como o orçamento é montado (decisão de 18/11/2026):
/// Unico = itens exatos do catálogo (ex.: loja de pijamas por catálogo);
/// Comparativo = até 3 opções com produtos EQUIVALENTES de marcas/preços diferentes (ex.: papelaria).
/// </summary>
public enum ModoOrcamento
{
    Unico = 1,
    Comparativo = 2
}

/// <summary>
/// De onde veio um preço. Regra central do sistema: nenhum preço existe sem origem.
/// </summary>
public enum OrigemPreco
{
    /// <summary>Retornado por integração real com fornecedor/marketplace.</summary>
    Integracao = 1,
    /// <summary>Tabela de preço cadastrada para o fornecedor (ProdutoFornecedor).</summary>
    FornecedorCadastrado = 2,
    /// <summary>Digitado pelo usuário.</summary>
    Manual = 3,
    /// <summary>Calculado a partir de histórico/preço de referência — não é cotação.</summary>
    Estimado = 4,
    /// <summary>Gerado pelo provedor simulado de testes. Nunca usar para compra.</summary>
    Simulado = 5,
    /// <summary>Preço de venda cadastrado pela própria loja no produto (é o preço que o cliente final paga).</summary>
    PrecoVenda = 6
}

public enum TipoMovimentacaoEstoque
{
    Entrada = 1,
    Saida = 2,
    /// <summary>Correção de inventário: define o saldo para um valor absoluto.</summary>
    Ajuste = 3
}

public enum FormatoImportacao
{
    Pdf = 1,
    Imagem = 2,
    Excel = 3,
    Word = 4,
    Texto = 5,
    WhatsApp = 6
}

public enum StatusImportacao
{
    Processando = 1,
    AguardandoRevisao = 2,
    Revisada = 3,
    Erro = 4
}

public enum NivelLog
{
    Informacao = 1,
    Aviso = 2,
    Erro = 3,
    Auditoria = 4
}
