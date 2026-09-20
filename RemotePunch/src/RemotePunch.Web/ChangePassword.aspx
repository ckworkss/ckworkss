<%@ Page Title="Change password" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="ChangePassword.aspx.cs" Inherits="RemotePunch.Web.ChangePasswordPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <div class="auth-wrap">
        <div class="card">
            <h1 class="auth-title">Change your password</h1>
            <p class="auth-sub">At least 10 characters, mixing upper case, lower case, digits or symbols.</p>

            <asp:PlaceHolder ID="phForced" runat="server" Visible="false">
                <div class="alert alert-warn">Set a new password before you can punch.</div>
            </asp:PlaceHolder>
            <asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
                <div class="alert <%= MessageClass %>"><asp:Literal ID="litMessage" runat="server" /></div>
            </asp:PlaceHolder>

            <div class="field">
                <label for="<%= txtCurrent.ClientID %>">Current password</label>
                <asp:TextBox ID="txtCurrent" runat="server" TextMode="Password" autocomplete="current-password" />
            </div>
            <div class="field">
                <label for="<%= txtNew.ClientID %>">New password</label>
                <asp:TextBox ID="txtNew" runat="server" TextMode="Password" autocomplete="new-password" />
            </div>
            <div class="field">
                <label for="<%= txtConfirm.ClientID %>">Confirm new password</label>
                <asp:TextBox ID="txtConfirm" runat="server" TextMode="Password" autocomplete="new-password" />
            </div>

            <asp:Button ID="btnSave" runat="server" Text="Save password" CssClass="btn btn-primary"
                        Style="width:100%" OnClick="btnSave_Click" />
        </div>
    </div>
</asp:Content>
