using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Admin
{
    public partial class DevicesPage : Page
    {
        protected Repeater rptDevices;
        protected PlaceHolder phEmpty;
        protected PlaceHolder phMessage;
        protected Literal litMessage;

        private readonly DeviceRepository _devices = new DeviceRepository();

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();
            if (!IsPostBack) Bind();
        }

        private void Bind()
        {
            List<Device> devices = _devices.GetAll(null);
            rptDevices.DataSource = devices;
            rptDevices.DataBind();
            rptDevices.Visible = devices.Count > 0;
            phEmpty.Visible = devices.Count == 0;
        }

        protected void rptDevices_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            AppUser actor = AppUser.Require();

            int deviceId;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), NumberStyles.Integer,
                              CultureInfo.InvariantCulture, out deviceId))
            {
                return;
            }

            Device device = _devices.GetById(deviceId);
            if (device == null) return;

            bool trusted = device.IsTrusted;
            bool blocked = device.IsBlocked;

            switch ((e.CommandName ?? string.Empty).ToLowerInvariant())
            {
                case "trust": trusted = true; blocked = false; break;
                case "untrust": trusted = false; break;
                case "block": blocked = true; trusted = false; break;
                case "unblock": blocked = false; break;
                default: return;
            }

            _devices.SetFlags(deviceId, trusted, blocked);
            new AuditRepository().Write(actor.EmployeeId, "DeviceUpdated",
                "Device #" + deviceId.ToString(CultureInfo.InvariantCulture) +
                " trusted=" + trusted + " blocked=" + blocked, ClientInfo.FromRequest(Request));

            phMessage.Visible = true;
            litMessage.Text = "Device updated.";
            Bind();
        }

        protected string ShortFingerprint(object value)
        {
            string text = Convert.ToString(value);
            if (string.IsNullOrEmpty(text)) return "&mdash;";
            return PageHelpers.Encode(text.Length > 16 ? text.Substring(0, 16) + "…" : text);
        }

        protected string LocalTime(object value)
        {
            if (value == null || value == DBNull.Value) return "&mdash;";
            return AppConfig.ToLocal(Convert.ToDateTime(value))
                .ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        }

        protected string StateBadge(object dataItem)
        {
            Device device = dataItem as Device;
            if (device == null) return string.Empty;
            if (device.IsBlocked) return "<span class=\"badge badge-bad\">blocked</span>";
            if (device.IsTrusted) return "<span class=\"badge badge-ok\">trusted</span>";
            return "<span class=\"badge badge-warn\">untrusted</span>";
        }
    }
}
