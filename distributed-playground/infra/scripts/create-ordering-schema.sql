-- Schema and tables for Ordering context (from EF migration InitialCreate)
CREATE SCHEMA IF NOT EXISTS ordering;

CREATE TABLE IF NOT EXISTS ordering."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS ordering.orders (
    "Id" uuid NOT NULL,
    "CustomerId" uuid NOT NULL,
    "CustomerReference" character varying(100),
    "RequestedDeliveryDate" date,
    "Priority" integer NOT NULL,
    "CurrencyCode" character varying(3) NOT NULL,
    "PaymentTerms" character varying(50),
    "ShippingMethod" character varying(50),
    shipping_recipient_name character varying(200),
    shipping_address_line1 character varying(200),
    shipping_address_line2 character varying(200),
    shipping_city character varying(100),
    shipping_state character varying(100),
    shipping_postal_code character varying(20),
    shipping_country_code character varying(3),
    shipping_phone character varying(30),
    shipping_notes character varying(500),
    "Notes" character varying(2000),
    "Status" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "TrackingNumber" character varying(100),
    "Carrier" character varying(100),
    "EstimatedDeliveryDate" date,
    "ShippedAt" timestamp with time zone,
    "DeliveredAt" timestamp with time zone,
    "ReceivedBy" character varying(200),
    "DeliveryNotes" character varying(1000),
    "InvoiceId" uuid,
    "InvoicedAt" timestamp with time zone,
    "CancellationReason" character varying(1000),
    "CancelledAt" timestamp with time zone,
    CONSTRAINT "PK_orders" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS ordering.order_lines (
    "Id" uuid NOT NULL,
    "LineNumber" integer NOT NULL,
    "ProductCode" character varying(50) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "UnitOfMeasure" character varying(10) NOT NULL,
    "UnitPrice" numeric(18,4) NOT NULL,
    "DiscountPercent" numeric(5,2) NOT NULL,
    "TaxPercent" numeric(5,2) NOT NULL,
    "OrderId" uuid,
    CONSTRAINT "PK_order_lines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_order_lines_orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES ordering.orders ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_order_lines_OrderId_LineNumber" ON ordering.order_lines ("OrderId", "LineNumber");
CREATE INDEX IF NOT EXISTS "IX_orders_CreatedAt" ON ordering.orders ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_orders_CustomerId" ON ordering.orders ("CustomerId");
CREATE INDEX IF NOT EXISTS "IX_orders_Status" ON ordering.orders ("Status");

INSERT INTO ordering."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260130175217_InitialCreate', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
