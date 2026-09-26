<%@ Page Title="Diagnostics" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Diagnostics.aspx.cs" Inherits="RemotePunch.Web.DiagnosticsPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Sign-in diagnostics</h1>

    <div class="alert alert-warn">
        <strong>Temporary tool.</strong> It runs only when <code>Diag.Enabled</code> is
        <code>true</code> in Web.config and only for requests from the server itself.
        Set it back to <code>false</code> when you are done.
    </div>

    <div class="card">
        <h2>What this application is connected to</h2>
        <div class="geo-readout"><asp:Literal ID="litConnection" runat="server" /></div>
    </div>

    <div class="card">
        <h2>Test a password</h2>
        <p class="muted small">
            This calls the same <code>PasswordHasher.Verify</code> the sign-in form calls,
            against the same row it loads. The password is never stored or logged.
        </p>
        <div class="filters">
            <div class="field">
                <label for="<%= txtCode.ClientID %>">Employee code or email</label>
                <asp:TextBox ID="txtCode" runat="server" Text="CKJHA" MaxLength="200" />
            </div>
            <div class="field">
                <label for="<%= txtPassword.ClientID %>">Password</label>
                <asp:TextBox ID="txtPassword" runat="server" TextMode="Password" MaxLength="200" />
            </div>
            <div class="actions">
                <asp:Button ID="btnTest" runat="server" Text="Test" CssClass="btn btn-primary" OnClick="btnTest_Click" />
            </div>
        </div>
        <asp:PlaceHolder ID="phResult" runat="server" Visible="false">
            <div class="geo-readout"><asp:Literal ID="litResult" runat="server" /></div>
        </asp:PlaceHolder>
    </div>

    <div class="card">
        <h2>Accounts in this database</h2>
        <div class="table-wrap"><asp:Literal ID="litEmployees" runat="server" /></div>
    </div>
</asp:Content>
