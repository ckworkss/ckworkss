<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="ArticleOcr.Web.Default" ValidateRequest="false" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Roll Label OCR</title>
    <link rel="stylesheet" href="Content/site.css" />
</head>
<body>
    <form id="form1" runat="server" enctype="multipart/form-data">
        <header>
            <h1>Roll label OCR</h1>
            <p>Photograph a roll label, read the article code; when the print is unreadable the code is inferred from the master list and the article-code pattern.</p>
        </header>

        <section class="card">
            <h2>1. Read a label photo</h2>
            <asp:FileUpload ID="fuImage" runat="server" accept="image/*" capture="environment" />
            <asp:Button ID="btnOcr" runat="server" Text="Read label" CssClass="primary" OnClick="btnOcr_Click" />
            <asp:CheckBox ID="chkDebug" runat="server" Text="show OCR text and diagnostics" Checked="true" />
        </section>

        <section class="card">
            <h2>2. Or guess from text</h2>
            <p class="hint">Paste a damaged code (e.g. <code>R0258I5O</code>) or a whole label text; the guesser ranks the most likely article codes.</p>
            <asp:TextBox ID="txtGuess" runat="server" TextMode="MultiLine" Rows="3" Columns="60" />
            <asp:Button ID="btnGuess" runat="server" Text="Guess article" OnClick="btnGuess_Click" />
        </section>

        <asp:Panel ID="pnlError" runat="server" CssClass="card error" Visible="false">
            <asp:Literal ID="litError" runat="server" />
        </asp:Panel>

        <asp:Panel ID="pnlResult" runat="server" CssClass="card result" Visible="false">
            <asp:Literal ID="litResult" runat="server" />
        </asp:Panel>

        <footer>
            <p>JSON API: <code>POST Api/Ocr.ashx</code> with a multipart <code>image</code> file, or <code>GET Api/Ocr.ashx?text=R0258I5O</code>. Add <code>debug=1</code> for OCR text and diagnostics.</p>
        </footer>
    </form>
</body>
</html>
