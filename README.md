<img width="298" height="673" alt="image" src="https://github.com/user-attachments/assets/2a267707-8556-4280-a350-ef26bdb4f081" />

### Promotions and loyalty points

The Manager Panel includes promotion creation for discount codes, minimum-spend tiers,
Flash Sales, and buy-X-get-Y offers. Promotions are applied in the cart and carried
through checkout. Loyalty points accrue at 1 point per ฿100 spent; 500 points can be
redeemed for a 5% cart discount. Each user account can use a discount code once;
cancelled orders do not count as a redemption.

The Manager Panel also provides date-filtered sales reports for paid orders and
account search/role management, including assigning Staff access. User accounts are
retained to preserve order history, and managers cannot remove their own Manager role
or demote the last Manager account.

Staff can view paid orders, start Pick & Pack (which checks and deducts stock once),
receive inventory, and confirm shipment with a tracking number. Customers can see the
shipping status and tracking number in their profile.
Shipment confirmation also creates an in-app notification for the customer, shown in
the navigation bar until opened.

The application applies EF Core migrations at startup. Ensure the configured MySQL
database is reachable when the application starts.

