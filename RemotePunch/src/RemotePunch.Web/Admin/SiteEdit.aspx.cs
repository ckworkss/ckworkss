using System;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Admin
{
    public partial class SiteEditPage : Page
    {
        protected Literal litTitle, litMessage;
        protected PlaceHolder phMessage;
        protected TextBox txtName, txtAddress, txtLatitude, txtLongitude, txtRadius;
        protected CheckBox chkActive;
        protected Button btnSave;

        private readonly SiteRepository _sites = new SiteRepository();

        private int SiteId
        {
            get { return PageHelpers.ParseInt(Request.QueryString["id"]).GetValueOrDefault(); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            AppUser.Require();

            if (!IsPostBack)
            {
                litTitle.Text = SiteId > 0 ? "Edit site" : "Add site";
                if (SiteId > 0) Load();
            }
        }

        private void Load()
        {
            Site site = _sites.GetById(SiteId);
            if (site == null)
            {
                Response.Redirect("~/Admin/Sites.aspx", true);
                return;
            }

            txtName.Text = site.Name;
            txtAddress.Text = site.Address;
            txtLatitude.Text = site.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            txtLongitude.Text = site.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            txtRadius.Text = site.RadiusMeters.ToString(CultureInfo.InvariantCulture);
            chkActive.Checked = site.IsActive;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            AppUser actor = AppUser.Require();

            string name = txtName.Text.Trim();
            if (name.Length == 0)
            {
                Show("The site needs a name.");
                return;
            }

            double latitude, longitude;
            if (!double.TryParse(txtLatitude.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                !double.TryParse(txtLongitude.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out longitude) ||
                !GeoMath.IsPlausibleCoordinate(latitude, longitude))
            {
                Show("Enter a real latitude and longitude, or pick the centre on the map.");
                return;
            }

            int radius;
            if (!int.TryParse(txtRadius.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out radius) ||
                radius < 25 || radius > 100000)
            {
                Show("The radius must be between 25 and 100000 metres.");
                return;
            }

            Site site = new Site
            {
                SiteId = SiteId,
                Name = name,
                Address = txtAddress.Text.Trim(),
                Latitude = latitude,
                Longitude = longitude,
                RadiusMeters = radius,
                IsActive = chkActive.Checked
            };

            if (SiteId > 0) _sites.Update(site); else _sites.Insert(site);

            new AuditRepository().Write(actor.EmployeeId, SiteId > 0 ? "SiteUpdated" : "SiteCreated",
                name + " @ " + latitude.ToString("F6", CultureInfo.InvariantCulture) + "," +
                longitude.ToString("F6", CultureInfo.InvariantCulture) +
                " r=" + radius.ToString(CultureInfo.InvariantCulture) + "m",
                ClientInfo.FromRequest(Request));

            Response.Redirect("~/Admin/Sites.aspx", true);
        }

        private void Show(string message)
        {
            phMessage.Visible = true;
            litMessage.Text = Server.HtmlEncode(message);
        }
    }
}
