<%@ Page Title="Something went wrong" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Error.aspx.cs" Inherits="RemotePunch.Web.ErrorPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <div class="auth-wrap">
        <div class="card">
            <h1 class="auth-title">Something went wrong</h1>
            <p class="auth-sub">
                The problem has been written to the audit log. Try again, and tell
                IT if it keeps happening.
            </p>
            <div class="actions" style="justify-content:center">
                <a class="btn btn-primary" href="<%= ResolveUrl("~/Punch.aspx") %>">Back to punching</a>
            </div>
        </div>
    </div>
</asp:Content>
