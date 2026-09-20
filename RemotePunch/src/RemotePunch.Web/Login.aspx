<%@ Page Title="Sign in" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Login.aspx.cs" Inherits="RemotePunch.Web.LoginPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <div class="auth-wrap">
        <div class="card">
            <h1 class="auth-title">Sign in</h1>
            <p class="auth-sub">Use your employee code or work email.</p>

            <asp:PlaceHolder ID="phError" runat="server" Visible="false">
                <div class="alert alert-bad"><asp:Literal ID="litError" runat="server" /></div>
            </asp:PlaceHolder>

            <div class="field">
                <label for="<%= txtUser.ClientID %>">Employee code or email</label>
                <asp:TextBox ID="txtUser" runat="server" autocomplete="username" MaxLength="200" />
            </div>

            <div class="field">
                <label for="<%= txtPassword.ClientID %>">Password</label>
                <asp:TextBox ID="txtPassword" runat="server" TextMode="Password"
                             autocomplete="current-password" MaxLength="200" />
            </div>

            <div class="checkline">
                <asp:CheckBox ID="chkRemember" runat="server" />
                <label for="<%= chkRemember.ClientID %>" style="margin:0">Keep me signed in on this device</label>
            </div>

            <asp:Button ID="btnSignIn" runat="server" Text="Sign in" CssClass="btn btn-primary"
                        Style="width:100%" OnClick="btnSignIn_Click" />

            <p class="small muted" style="margin-top:16px">
                Punching needs location access. Open this site over https and allow
                location when your browser asks.
            </p>
        </div>
    </div>
</asp:Content>
