# Guía de entrevista — microservices-demo

Guía en español para **entender el proyecto de punta a punta** y **practicar entrevistas** sobre él. Las preguntas
van de menor a mayor dificultad: primero las que haría alguien que no programa y después las de nivel junior,
semisenior, senior y arquitecto. Cada respuesta dice **dónde está el código** e incluye un **fragmento real** del
repositorio.

> Los fragmentos están copiados del código y recortados con `// ...` para que se lean rápido. Los nombres de
> clases, archivos y comentarios del código quedan en inglés, como en el repositorio. Si un fragmento no coincide
> con el código actual, manda el código: esta guía se escribió sobre la rama `main` de septiembre de 2026.

## Cómo usar esta guía

- **Para estudiar:** lee cada nivel de corrido y abre el archivo enlazado en tu editor. Entender *por qué* el código
  es así vale más que memorizar la respuesta.
- **Para practicar:** tapa la respuesta, contesta en voz alta y compara. La **respuesta corta** es lo que dirías en
  30 segundos; la **explicación** es lo que agregarías si te piden más.
- **Repreguntas:** al final de muchas respuestas hay preguntas de seguimiento típicas. Suelen ser las que separan a
  quien leyó el código de quien lo entendió.

## Índice

- [Glosario mínimo](#glosario-mínimo)
- [Nivel 0 — Preguntas de alguien que no programa](#nivel-0--preguntas-de-alguien-que-no-programa)
- [Nivel 1 — Junior](#nivel-1--junior)
- [Nivel 2 — Semisenior](#nivel-2--semisenior)
- [Nivel 3 — Senior](#nivel-3--senior)
- [Nivel 4 — Arquitecto](#nivel-4--arquitecto)
- [Cierre: pitches, preguntas difíciles y mapa rápido](#cierre-pitches-preguntas-difíciles-y-mapa-rápido)

## Glosario mínimo

| Término | En una frase |
|---|---|
| **API** | La "ventanilla" de un programa: otro programa le hace pedidos (por HTTP) y recibe respuestas. |
| **Microservicio** | Un programa chico que hace una sola cosa del negocio (catálogo, pedidos, stock...) y tiene sus propios datos. |
| **Base de datos** | Donde se guardan los datos de forma permanente. Aquí es PostgreSQL, una por servicio. |
| **Mensaje / evento** | Un aviso que un servicio deja en una "cartelera" (Azure Service Bus) para que otros lo lean cuando puedan: "se hizo un pedido". |
| **Gateway** | La puerta de entrada única: todos los pedidos del navegador pasan por ahí antes de llegar a los servicios. |
| **JWT (token)** | Una "credencial" firmada que dice quién eres; se envía en cada pedido para no repetir usuario y contraseña. |
| **Contenedor (Docker)** | Una caja que trae el programa y todo lo que necesita, para que funcione igual en cualquier máquina. |
| **SKU** | El código único de un producto, por ejemplo `GOLDEN-LAGER-350`. Es lo único que todos los servicios comparten. |
| **Idempotente** | Que hacerlo dos veces da el mismo resultado que hacerlo una. Clave cuando un mensaje llega repetido. |
| **Outbox / Inbox** | Tablas que garantizan que un evento no se pierde al enviarlo (outbox) ni se procesa dos veces al recibirlo (inbox). |

---

## Nivel 0 — Preguntas de alguien que no programa

### 0.1 ¿De qué trata el proyecto?

**Respuesta corta:** es una plataforma para que **bares y almacenes le pidan cerveza a un distribuidor** por
internet, inspirada en BEES (la app B2B de AB InBev). El punto de venta inicia sesión, ve el catálogo, arma un
carrito y hace el pedido. El sistema revisa el stock, confirma o rechaza el pedido y avisa por un panel y por e-mail.

**Explicación:** "B2B" significa *business to business*: el cliente no es una persona que compra una lata, sino un
negocio que compra **packs** para revender. Por eso los precios son por pack y hay descuentos por volumen. Los
productos son inventados, con un guiño a Los Simpsons.

**Dónde está en el código:** [CatalogSeeder.cs](../src/Services/Catalog/Catalog.Api/Persistence/CatalogSeeder.cs#L34)
carga los productos de ejemplo.

```csharp
public static IReadOnlyList<Product> CreateProducts() =>
[
    Product.Create("DUFF-STYLE-CLASSIC-350", "Duff-Style Classic Lager 350ml", BeerStyle.Lager, 350, 12, 14.40m),
    Product.Create("GOLDEN-LAGER-350", "Golden Lager 350ml", BeerStyle.Lager, 350, 12, 12.00m),
    // ...
    Product.Create("AMBER-ALE-600", "Amber Ale 600ml", BeerStyle.AmberAle, 600, 12, 27.60m),
    Product.Create("HOPPY-TRAIL-IPA-473", "Hoppy Trail IPA 473ml", BeerStyle.Ipa, 473, 6, 17.70m),
    Product.Create("MIDNIGHT-STOUT-500", "Midnight Stout 500ml", BeerStyle.Stout, 500, 6, 19.50m),
];
```

Cada línea es un producto: código (SKU), nombre, estilo, mililitros, unidades por pack y precio del pack.

### 0.2 ¿Qué problema resuelve?

**Respuesta corta:** que un distribuidor reciba pedidos de cientos de puntos de venta **sin vender lo que no
tiene**, con precios y descuentos calculados siempre igual, y que el cliente sepa enseguida si su pedido salió o no.

**Explicación:** hay tres riesgos que el sistema evita:

1. **Vender stock que no existe.** El stock se reserva *antes* de confirmar, y es "todo o nada": si falta un
   producto, no se reserva ninguno.
2. **Que el cliente elija su precio.** El navegador solo envía qué productos y cuántos; el precio lo toma el
   servidor del catálogo en ese momento.
3. **Perder pedidos cuando algo falla.** Si un servicio se cae, los avisos esperan en la cola y se procesan cuando
   vuelve.

**Dónde está en el código:** el navegador solo manda SKU y cantidad
([endpoints.ts](../src/Web/src/api/endpoints.ts#L31)).

```ts
/**
 * Only SKUs and quantities are sent: Ordering snapshots the price from Catalog and takes the customer
 * from the token, so neither can be chosen by this app.
 */
export const placeOrder = (call: ApiRequest, items: PlaceOrderItem[]): Promise<Order> =>
  call<Order>('/api/orders', { method: 'POST', body: { items } })
```

### 0.3 ¿Cómo funciona, paso a paso?

**Respuesta corta:** como una cocina de restaurante con comandas. El mozo (Ordering) anota el pedido y dice "ya lo
paso"; deja la comanda en la cartelera (Service Bus); el depósito (Inventory) la lee, aparta la mercadería y deja
otra nota: "listo" o "no hay"; el mozo marca el pedido como confirmado o rechazado; y el encargado de avisos
(Notifications) le escribe al cliente.

**Explicación, con los nombres reales:**

1. El punto de venta inicia sesión en el **Gateway** y recibe un token.
2. Envía el pedido. **Ordering** le pide los precios a **Catalog**, guarda el pedido como `Pending` y responde
   enseguida "recibido" (HTTP 202).
3. Ordering publica el evento `OrderPlaced`.
4. **Inventory** lo recibe, intenta reservar y publica `StockReserved` o `StockRejected`.
5. Ordering cambia el pedido a `Confirmed` o `Rejected` y publica `OrderConfirmed` u `OrderRejected`.
6. **Notifications** guarda la notificación y envía el e-mail.
7. Mientras tanto, la página del pedido pregunta cada 2 segundos cómo va, hasta que el estado es final.

**Dónde está en el código:** el pedido nace `Pending` y solo puede pasar a `Confirmed` o `Rejected`
([Order.cs](../src/Services/Ordering/Ordering.Domain/Orders/Order.cs#L21)).

```csharp
private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> _allowedTransitions = new Dictionary<OrderStatus, OrderStatus[]>
{
    [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Rejected],
    [OrderStatus.Confirmed] = [],
    [OrderStatus.Rejected] = [],
};
```

Se lee así: desde "pendiente" se puede ir a "confirmado" o "rechazado"; desde esos dos no se puede ir a ningún lado.

### 0.4 ¿Qué ve el usuario?

**Respuesta corta:** una web con cinco pantallas: **login**, **catálogo** (con el stock disponible), **carrito**,
**pedidos** (con el estado que se actualiza solo) y **notificaciones**.

**Dónde está en el código:** las páginas están en [src/Web/src/pages](../src/Web/src/pages). La del detalle del
pedido muestra un mensaje distinto según el estado
([OrderDetailsPage.tsx](../src/Web/src/pages/OrderDetailsPage.tsx#L45)).

```tsx
{order.status === 'Pending' && (
  <p className="notice notice-info">
    Waiting for the warehouse to reserve the stock. This page updates itself.
  </p>
)}

{order.status === 'Rejected' && (
  <p className="notice notice-error" role="alert">
    {order.rejectionReason ?? 'The order was rejected.'}
  </p>
)}
```

### 0.5 ¿Qué pasa si no hay stock?

**Respuesta corta:** el pedido se **rechaza completo** y el cliente ve el motivo y qué producto faltó. No se
reserva nada, ni siquiera lo que sí había. En la demo, *Midnight Stout 500ml* tiene solo 5 packs para poder mostrarlo.

**Dónde está en el código:** la regla "todo o nada" está en
[StockReservation.cs](../src/Services/Inventory/Inventory.Api/Domain/StockReservation.cs#L39): primero revisa
todas las líneas y solo después reserva.

```csharp
foreach (var (sku, quantity) in requestedBySku)
{
    // ...
    if (!itemsBySku.TryGetValue(sku, out var item))
    {
        unknownSkus.Add(sku);
    }
    else if (!item.CanReserve(quantity))
    {
        insufficientSkus.Add(sku);
    }
}

if (unknownSkus.Count > 0 || insufficientSkus.Count > 0)
{
    // ...
    return ReservationOutcome.Rejected(reason, [.. unknownSkus.Concat(insufficientSkus).Order(StringComparer.Ordinal)]);
}

foreach (var (sku, quantity) in requestedBySku)
{
    itemsBySku[sku].Reserve(quantity, utcNow);
}
```

### 0.6 ¿Por qué el pedido tarda unos segundos en confirmarse?

**Respuesta corta:** porque la confirmación la decide **otro servicio**, que trabaja por su cuenta. El pedido se
acepta al instante ("lo recibimos") y la respuesta final llega un par de segundos después. Es a propósito: si el
depósito está lento o caído, el cliente igual puede hacer su pedido y nada se pierde.

**Dónde está en el código:** la web pregunta cada 2 segundos y deja de preguntar cuando el estado ya no es
`Pending` ([OrderDetailsPage.tsx](../src/Web/src/pages/OrderDetailsPage.tsx#L10)).

```tsx
const pendingRefreshMs = 2_000

// ...
const { data: order, isLoading, error, refresh } = usePolledResource(load, {
  intervalMs: pendingRefreshMs,
  isFinal: (current) => current.status !== 'Pending',
})
```

### 0.7 ¿Cómo sabes que funciona?

**Respuesta corta:** con **pruebas automáticas** que se ejecutan en cada cambio (GitHub Actions): pruebas de las
reglas de negocio, pruebas contra una base de datos real en un contenedor, pruebas del Gateway y pruebas que
verifican que la arquitectura no se rompa. Además hay colecciones de Postman para probar a mano cada servicio.

**Dónde está en el código:** un test del descuento por volumen
([VolumeDiscountPolicyTests.cs](../tests/Ordering.Domain.UnitTests/Discounts/VolumeDiscountPolicyTests.cs#L22)).

```csharp
[Theory]
[InlineData(10, 10.00)]
[InlineData(49, 10.00)]
[InlineData(50, 20.00)]
[InlineData(500, 20.00)]
public void CalculateDiscount_TierReached_AppliesOnlyHighestTierRate(int totalQuantity, decimal expected)
{
    var discount = _policy.CalculateDiscount(Usd(200m), totalQuantity);

    discount.ShouldBe(Usd(expected));
}
```

Se lee: "con 10 packs el descuento de 200 dólares es 10; con 50 packs es 20".

### 0.8 ¿Cómo se pone en marcha?

**Respuesta corta:** con **un solo comando**. .NET Aspire levanta las bases de datos, la cola de mensajes, el
capturador de e-mails, los cinco servicios y la web, y abre un panel para ver todo funcionando.

```bash
dotnet run --project src/AppHost
```

**Dónde está en el código:** [AppHost.cs](../src/AppHost/AppHost.cs#L13) declara cada pieza.

```csharp
// PostgreSQL: one server container, one database per service (database-per-service pattern).
var postgres = builder.AddPostgres("postgres")
    // ...
var catalogDb = postgres.AddDatabase("catalogdb");
var orderingDb = postgres.AddDatabase("orderingdb");
var inventoryDb = postgres.AddDatabase("inventorydb");
var notificationsDb = postgres.AddDatabase("notificationsdb");
```

También se puede levantar con `docker compose up --build` (ver el README).

### 0.9 ¿Qué tiene que ver Azure, "la nube"?

**Respuesta corta:** el proyecto usa dos servicios de Microsoft Azure: **Service Bus** (la cartelera de mensajes) y
**Azure Functions** (el servicio de notificaciones). Para no pagar nada ni necesitar cuenta, en local se usan
**emuladores** oficiales que corren en Docker.

**Dónde está en el código:** [AppHost.cs](../src/AppHost/AppHost.cs#L28).

```csharp
// Azure Service Bus emulator. Topics, subscriptions and filters come from the shared Topology,
// so the infrastructure and the code can never disagree about names.
var serviceBus = builder.AddAzureServiceBus(Topology.ServiceBusConnectionName)
    .RunAsEmulator(emulator => emulator
        .WithContainerName($"{ContainerPrefix}servicebus")
        // ...
```

### 0.10 ¿Qué pasa con los e-mails? ¿Llegan de verdad?

**Respuesta corta:** por defecto **no salen a internet**: los atrapa **Mailpit**, una bandeja de prueba que se ve
en `http://localhost:8025`. Con una configuración de Gmail sí llegan a una casilla real.

**Dónde está en el código:** [AppHost.cs](../src/AppHost/AppHost.cs#L67).

```csharp
// Mail catcher: Mailpit speaks real SMTP and shows every message it receives in a web UI, so the demo can show
// an e-mail leaving the system without anybody owning a mail account. Swapping Email:Host/Port for Gmail is enough
// to deliver for real (see .env.example).
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.31")
```

La pregunta técnica completa está en [2.3 ¿Cómo se envía un e-mail?](#23-cómo-se-envía-un-e-mail).

### 0.11 ¿Por qué no hay marcas reales?

**Respuesta corta:** por prolijidad legal: es una demo pública y no debe parecer oficial de ninguna marca. Todos los
productos son inventados; el único guiño es "Duff-Style", por la cerveza de Los Simpsons.

**Dónde está en el código:** la regla está escrita en [CLAUDE.md](../CLAUDE.md) ("Catalog data is fictional") y
en el comentario del seeder ([CatalogSeeder.cs](../src/Services/Catalog/Catalog.Api/Persistence/CatalogSeeder.cs#L6)).

```csharp
/// <summary>
/// Fictional demo catalog, plugged into EF Core <c>UseSeeding</c>/<c>UseAsyncSeeding</c>:
/// it runs right after migrations are applied and only inserts into an empty table.
/// </summary>
/// <remarks>Inventory seeds its stock with these same SKUs.</remarks>
public static class CatalogSeeder
```

---
## Nivel 1 — Junior

### 1.1 ¿Qué tecnologías usa el proyecto?

**Respuesta corta:** backend en **.NET 10 / C# 14** con ASP.NET Core, **PostgreSQL** con **EF Core**, mensajería
con **Azure Service Bus**, una **Azure Function**, **YARP** como gateway, **.NET Aspire** para orquestar en local,
**Docker** y **GitHub Actions**. El frontend es **React + Vite + TypeScript**.

**Dónde está en el código:** todas las versiones de NuGet están en un único archivo,
[Directory.Packages.props](../Directory.Packages.props#L31) (Central Package Management).

```xml
<!-- Messaging and persistence -->
<ItemGroup>
  <PackageVersion Include="Azure.Messaging.ServiceBus" Version="7.20.2" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.12" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />
</ItemGroup>
```

### 1.2 ¿Qué es una API REST y qué endpoints tiene el proyecto?

**Respuesta corta:** una API REST expone **recursos** (productos, pedidos, stock) en URLs y usa los **verbos HTTP**
para operar sobre ellos: `GET` lee, `POST` crea, `PUT` reemplaza. La respuesta indica el resultado con un código de
estado (200 OK, 201 Created, 202 Accepted, 400, 404...).

| Servicio | Endpoints |
|---|---|
| Gateway | `POST /auth/token` |
| Catalog | `GET /api/products`, `GET /api/products/{id}`, `POST` / `PUT /api/products` (Admin) |
| Ordering | `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}` |
| Inventory | `GET /api/stock`, `PUT /api/stock/{sku}` (Admin) |
| Notifications | `GET /api/notifications` |

**Dónde está en el código:** hacer un pedido devuelve **202 Accepted** (no 201) porque el pedido todavía no está
resuelto ([OrdersController.cs](../src/Services/Ordering/Ordering.Api/Controllers/OrdersController.cs#L21)).

```csharp
// 202 Accepted: the order is stored as Pending; confirmation happens asynchronously after Inventory answers.
// The Location header points to the order, which the client polls to follow its status.
[HttpPost]
// ...
    return result.IsSuccess
        ? AcceptedAtAction(nameof(GetOrderById), new { id = result.Value.Id }, result.Value)
        : Problem(result.Error);
```

**Repreguntas típicas:** ¿Por qué 202 y no 201? (201 dice "creado y listo"; 202 dice "lo recibí, el resultado llega
después", y el header `Location` indica dónde consultarlo.)

### 1.3 ¿Qué es EF Core y qué es una migración?

**Respuesta corta:** **EF Core** es el ORM de .NET: permite trabajar con la base de datos usando clases de C# y LINQ
en lugar de escribir SQL a mano. Una **migración** es un archivo de código generado que describe un cambio del
esquema (crear tabla, agregar columna) y se puede aplicar o revertir de forma versionada.

**Explicación:** en este proyecto las migraciones se aplican **al arrancar** cada servicio y el mapeo de cada tabla
está en una clase `IEntityTypeConfiguration<T>`, fuera de la entidad.

**Dónde está en el código:** [StockItemConfiguration.cs](../src/Services/Inventory/Inventory.Api/Persistence/StockItemConfiguration.cs#L9).

```csharp
public void Configure(EntityTypeBuilder<StockItem> stockItem)
{
    stockItem.ToTable(
        "stock_items",
        table => table.HasCheckConstraint(
            "ck_stock_items_quantities",
            "quantity_available >= 0 AND quantity_reserved >= 0"));

    stockItem.HasKey(s => s.Id);

    stockItem.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
    stockItem.Property(s => s.Sku).HasColumnName("sku").HasMaxLength(StockItem.SkuMaxLength);
    // ...
    stockItem.HasIndex(s => s.Sku).IsUnique().HasDatabaseName("ix_stock_items_sku");
}
```

Para crear una migración nueva:

```bash
dotnet ef migrations add <Nombre> --project src/Services/Inventory/Inventory.Api --output-dir Persistence/Migrations
```

### 1.4 ¿Por qué hay una base de datos por servicio?

**Respuesta corta:** para que cada servicio sea **dueño de sus datos**. Si dos servicios comparten tablas, un cambio
en una rompe al otro y ya no se pueden desplegar por separado. Aquí hay **un servidor** PostgreSQL con **cuatro
bases**, y ningún servicio lee la base de otro.

**Explicación:** lo único que cruza fronteras es el **SKU**. Por eso Inventory tiene su propio seeder con los mismos
SKUs que Catalog, en lugar de leer la tabla de productos.

**Dónde está en el código:** [AppHost.cs](../src/AppHost/AppHost.cs#L94): cada servicio recibe *solo* su base.

```csharp
var catalog = builder.AddProject<Projects.Catalog_Api>("catalog")
    .WithReference(catalogDb)
    // ...
var inventory = builder.AddProject<Projects.Inventory_Api>("inventory")
    .WithReference(inventoryDb)
    .WaitFor(inventoryDb)
    .WithReference(serviceBus)
    // ...
```

**Repreguntas típicas:** ¿Un servidor con cuatro bases no es trampa? (Es la separación lógica que importa; en
producción cada base podría ir a otro servidor sin tocar código, porque la cadena de conexión viene de configuración.)

### 1.5 ¿Cómo se valida lo que llega en un request?

**Respuesta corta:** con **FluentValidation**. Cada request tiene su clase validadora y un filtro la ejecuta
**antes** del handler. Si falla, se responde **400** con la lista de errores por campo y el handler ni se entera.

**Dónde está en el código:** el validador de crear producto
([CreateProductValidator.cs](../src/Services/Catalog/Catalog.Api/Features/Products/CreateProduct/CreateProductValidator.cs#L5))
y el filtro que lo ejecuta ([ValidationFilter.cs](../src/BuildingBlocks/BuildingBlocks.Web/Validation/ValidationFilter.cs#L11)).

```csharp
internal sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(request => request.Sku).ValidSku();
        RuleFor(request => request.Name).ValidName();
        RuleFor(request => request.Style).ValidStyle();
        RuleFor(request => request.VolumeMl).ValidVolumeMl();
        RuleFor(request => request.PackSize).ValidPackSize();
        RuleFor(request => request.Price).ValidPrice();
    }
}
```

```csharp
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // ...
        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (validation.IsValid)
        {
            return await next(context);
        }

        return TypedResults.ValidationProblem(validation.ToDictionary());
    }
}
```

**Repreguntas típicas:** ¿Por qué no Data Annotations (`[Required]`)? (Las reglas quedan en una clase testeable,
se pueden reutilizar como `ValidSku()` y no ensucian el DTO.)

### 1.6 ¿Qué es la inyección de dependencias y dónde se ve?

**Respuesta corta:** en lugar de que una clase cree sus dependencias con `new`, las **recibe en el constructor** y
el contenedor de .NET se las da. Así puedes cambiar la implementación (por ejemplo, un mock en un test) sin tocar la
clase. Cada registro tiene un **ciclo de vida**: `Singleton` (una para toda la app), `Scoped` (una por request o por
mensaje) o `Transient` (una nueva cada vez).

**Dónde está en el código:** el registro ([InventoryServices.cs](../src/Services/Inventory/Inventory.Api/Services/InventoryServices.cs#L11))
y el uso por constructor primario ([StockController.cs](../src/Services/Inventory/Inventory.Api/Controllers/StockController.cs#L16)).

```csharp
public static IServiceCollection AddInventoryServices(this IServiceCollection services)
{
    services.TryAddSingleton(TimeProvider.System);

    return services.AddScoped<IStockService, StockService>();
}
```

```csharp
public sealed class StockController(IStockService stockService) : ApiControllerBase
```

**Repreguntas típicas:** ¿Por qué `StockService` es `Scoped` y no `Singleton`? (Usa el `DbContext`, que es scoped;
un singleton lo capturaría y lo compartiría entre requests concurrentes.) ¿Para qué `TimeProvider`? (Para no leer
el reloj directamente y poder fijar la hora en los tests.)

### 1.7 ¿Qué es un DTO y por qué no devolver la entidad directamente?

**Respuesta corta:** un **DTO** (*Data Transfer Object*) es un objeto plano con la forma exacta de lo que viaja por
la API. Separarlo de la entidad evita exponer campos internos, permite cambiar la base sin romper el contrato y, en
las lecturas, deja que EF Core traiga solo las columnas necesarias.

**Dónde está en el código:** [ProductResponse.cs](../src/Services/Catalog/Catalog.Api/Features/Products/ProductResponse.cs#L6)
(un `record` inmutable).

```csharp
public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    BeerStyle Style,
    int VolumeMl,
    int PackSize,
    decimal Price)
```

### 1.8 ¿Cómo funciona el login?

**Respuesta corta:** el usuario manda usuario y contraseña a `POST /auth/token` en el Gateway. Si son correctos, el
Gateway devuelve un **JWT firmado** con HS256 que dice quién es (`sub`), su e-mail y su rol. La web lo envía en cada
llamada en el header `Authorization: Bearer ...`.

**Explicación:** hay tres cuentas de demo (`bar`, `market`, `admin`, contraseña `demo`). Son credenciales de demo,
no secretos, y por eso están en `appsettings.json`. La **clave de firma** sí es un secreto: Aspire la genera y la
guarda en user-secrets.

**Dónde está en el código:** [DemoTokenIssuer.cs](../src/Gateway/Authentication/DemoTokenIssuer.cs#L38).

```csharp
var accessToken = TokenHandler.CreateToken(new SecurityTokenDescriptor
{
    Issuer = options.Issuer,
    Audience = options.Audience,
    IssuedAt = issuedAt,
    NotBefore = issuedAt,
    Expires = expiresAt,
    Claims = new Dictionary<string, object>
    {
        [JwtClaimNames.Subject] = username,
        [JwtClaimNames.Email] = account.Email,
        [JwtClaimNames.Role] = account.Role,
        [JwtClaimNames.Name] = displayName,
    },
    SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
});
```

**Repreguntas típicas:** ¿Por qué `FixedTimeEquals` para comparar la contraseña? (Comparación en tiempo constante:
el tiempo de respuesta no revela cuántos caracteres acertaste.) ¿Esto es producción? (No: en producción emite un
proveedor de identidad como Entra ID o Keycloak; ver [4.8](#48-qué-cambiarías-para-llevarlo-a-producción).)

### 1.9 ¿Qué es Docker y qué hace docker-compose aquí?

**Respuesta corta:** Docker empaqueta cada servicio con su runtime en una **imagen**. `docker-compose.yml` levanta
todas las imágenes juntas (servicios, PostgreSQL, emulador de Service Bus, Mailpit, Azurite) con su configuración.
Es la alternativa a Aspire para correr todo "como en un servidor".

**Dónde está en el código:** cada servicio tiene un Dockerfile **multi-stage**: compila con la imagen del SDK y
ejecuta con la imagen liviana de ASP.NET ([Dockerfile de Ordering](../src/Services/Ordering/Ordering.Api/Dockerfile#L7)).

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# ...
RUN dotnet restore src/Services/Ordering/Ordering.Api/Ordering.Api.csproj
# ...
RUN dotnet publish src/Services/Ordering/Ordering.Api/Ordering.Api.csproj \
    --configuration $BUILD_CONFIGURATION --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ordering.Api.dll"]
```

**Repreguntas típicas:** ¿Por qué se copian primero solo los `.csproj`? (Para aprovechar la caché de capas: el
`restore` se repite solo si cambian las dependencias, no con cada cambio de código.) ¿Por qué `USER $APP_UID`? (Para
no correr como root.)

### 1.10 ¿Qué es .NET Aspire?

**Respuesta corta:** una herramienta de Microsoft para **orquestar el entorno de desarrollo** en C#. El proyecto
`AppHost` declara qué contenedores y proyectos hay y cómo se conectan; Aspire los levanta, les inyecta las cadenas
de conexión y muestra un **dashboard** con logs, trazas y métricas. `ServiceDefaults` agrega a cada servicio
OpenTelemetry, health checks, service discovery y resiliencia.

**Dónde está en el código:** [AppHost.cs](../src/AppHost/AppHost.cs#L100).

```csharp
// Ordering does not wait for Catalog on purpose: calls to it go through a resilience pipeline (retry,
// circuit breaker, timeouts), and a missing Catalog only fails order placement with a 503.
var ordering = builder.AddProject<Projects.Ordering_Api>("ordering")
    .WithReference(orderingDb)
    .WaitFor(orderingDb)
    .WithReference(serviceBus)
    .WaitFor(serviceBus)
    .WithReference(catalog)
    .WithEnvironment(JwtSigningKeyVariable, jwtSigningKey);
```

`WithReference(catalog)` es lo que permite que Ordering llame a `https+http://catalog` sin saber el puerto.

### 1.11 ¿Qué es un test unitario y cómo se nombran aquí?

**Respuesta corta:** un test unitario prueba **una unidad de lógica** aislada, sin base de datos ni red, y corre en
milisegundos. Aquí se usa **xUnit** + **Shouldly** (asserts legibles) + **NSubstitute** (mocks), y el nombre sigue
`Método_Estado_ResultadoEsperado`.

**Dónde está en el código:** [StockReservationTests.cs](../tests/Inventory.UnitTests/Domain/StockReservationTests.cs#L41).

```csharp
[Fact]
public void Reserve_OneLineShort_ReservesNothing()
{
    // All or nothing: the line that could be served must stay untouched.
    var lager = StockItem.Create("GOLDEN-LAGER-350", 100, _now);
    var stout = StockItem.Create("MIDNIGHT-STOUT-500", 5, _now);

    var outcome = StockReservation.Reserve(
        [lager, stout],
        [new ReservationLine("GOLDEN-LAGER-350", 10), new ReservationLine("MIDNIGHT-STOUT-500", 6)],
        _now);
    // ...
```

### 1.12 ¿Qué es `async`/`await` y por qué todo es asíncrono?

**Respuesta corta:** `await` libera el hilo mientras se espera algo lento (la base, la red, el SMTP) y retoma cuando
termina. Un servidor con pocos hilos atiende así miles de requests. Todo método que hace I/O es `async` y recibe un
`CancellationToken` para cortar el trabajo si el cliente se va.

**Dónde está en el código:** [StockService.cs](../src/Services/Inventory/Inventory.Api/Services/StockService.cs#L35).

```csharp
public async Task<Result<StockResponse>> UpdateQuantityAvailableAsync(string sku, UpdateStockRequest request, CancellationToken cancellationToken)
{
    var normalizedSku = StockItem.NormalizeSku(sku);

    var stockItem = await dbContext.StockItems.SingleOrDefaultAsync(item => item.Sku == normalizedSku, cancellationToken);
    // ...
    await dbContext.SaveChangesAsync(cancellationToken);

    return stockItem.ToResponse();
}
```

### 1.13 ¿Cómo responde la API cuando hay un error?

**Respuesta corta:** siempre con el mismo formato estándar, **ProblemDetails (RFC 9457)**: `status`, `title`,
`detail`, un `code` estable (por ejemplo `Orders.NotFound`) y el `traceId` para encontrar la traza en el dashboard.

**Dónde está en el código:** [ProblemDetailsExtensions.cs](../src/BuildingBlocks/BuildingBlocks.Web/Results/ProblemDetailsExtensions.cs#L20)
agrega `instance` y `traceId` a toda respuesta de error.

```csharp
public static IServiceCollection AddApiProblemDetails(this IServiceCollection services) =>
    services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        context.ProblemDetails.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    });
```

Del otro lado, la web lee ese formato en un solo lugar ([client.ts](../src/Web/src/api/client.ts#L14)).

---
## Nivel 2 — Semisenior

### 2.1 ¿Qué diferencia hay entre minimal APIs y controladores? ¿Por qué hay de los dos?

**Respuesta corta:** los dos son formas de primera clase de exponer HTTP en ASP.NET Core 10.

- **Minimal APIs:** se declara la ruta con una lambda (`app.MapPost(...)`) y los filtros son `IEndpointFilter`.
  Tienen menos ceremonia y respuestas tipadas (`Results<Ok<T>, ProblemHttpResult>`) que documentan el OpenAPI solas.
- **Controladores MVC:** clases con atributos (`[HttpGet]`, `[Route]`), model binding, filtros de acción y las
  convenciones de `[ApiController]`. Es lo que usan la mayoría de las bases de código .NET existentes.

Aquí **Catalog y el Gateway usan minimal APIs** y **Ordering e Inventory usan controladores** (ADR 0007), con una
regla: **nunca se mezclan dentro de un servicio**. Los dos estilos comparten el mismo contrato de errores y de
validación, así que un cliente no nota la diferencia.

**Dónde está en el código:** la misma idea, "si falla devuelvo ProblemDetails", en los dos estilos.

Minimal API ([CreateProductEndpoint.cs](../src/Services/Catalog/Catalog.Api/Features/Products/CreateProduct/CreateProductEndpoint.cs#L10)):

```csharp
internal sealed class CreateProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ProductsApi.Route, async Task<Results<CreatedAtRoute<ProductResponse>, ProblemHttpResult>> (
                CreateProductRequest request,
                CreateProductHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);

                return result.IsSuccess
                    ? TypedResults.CreatedAtRoute(result.Value, GetProductByIdEndpoint.Name, new { id = result.Value.Id })
                    : result.ToProblem();
            })
            .RequireAdmin()
            .WithRequestValidation<CreateProductRequest>()
            // ...
}
```

Controlador ([StockController.cs](../src/Services/Inventory/Inventory.Api/Controllers/StockController.cs#L31)):

```csharp
[HttpPut("{sku}")]
[RequireAdmin]
[EndpointName("UpdateStock")]
// ...
public async Task<ActionResult<StockResponse>> UpdateStock(
    string sku,
    UpdateStockRequest request,
    CancellationToken cancellationToken)
{
    var result = await stockService.UpdateQuantityAvailableAsync(sku, request, cancellationToken);
    return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
}
```

La pieza que los mantiene iguales es la tabla `Error → status` compartida
([ErrorProblem.cs](../src/BuildingBlocks/BuildingBlocks.Web/Results/ErrorProblem.cs#L12)), que usan tanto
`ToProblem()` como `ApiControllerBase.Problem(error)`.

| | Minimal APIs | Controladores |
|---|---|---|
| Descubrimiento | `IEndpoint` + `AddEndpoints(assembly)` | `AddControllers()` + `MapControllers()` |
| Validación | `ValidationFilter<T>` por endpoint (`WithRequestValidation<T>()`) | `ValidationActionFilter` global |
| Errores | `result.ToProblem()` → `ProblemHttpResult` | `Problem(error)` → `ObjectResult` |
| Admin | `.RequireAdmin()` | `[RequireAdmin]` |

**Repreguntas típicas:** ¿Cuál elegirías para un servicio nuevo? (Minimal APIs para algo chico o de alto
rendimiento; controladores si el equipo ya los usa o si necesitas filtros y model binders complejos. Lo importante
es no mezclar dentro del mismo servicio.) ¿Hubo alguna diferencia visible al migrar? (Sí: un JSON mal formado ahora
lo responde `[ApiController]` con un 400 que nombra la ruta JSON; el status es el mismo.)

### 2.2 ¿Qué es el Result pattern y por qué no usar excepciones?

**Respuesta corta:** las **fallas esperadas** (validación, no encontrado, regla de negocio) se **devuelven como un
valor** `Result`/`Result<T>` con un `Error`, en lugar de lanzar una excepción. Las excepciones quedan para lo
realmente excepcional (un bug, la base caída).

**Explicación:** así el compilador obliga a mirar el resultado, el flujo se lee de arriba hacia abajo sin
`try/catch` y es más barato que lanzar. `Error.Type` permite que la capa HTTP decida el código de estado sin que el
dominio sepa nada de HTTP.

**Dónde está en el código:** [Result.cs](../src/BuildingBlocks/BuildingBlocks.Common/Results/Result.cs#L9) y
[Error.cs](../src/BuildingBlocks/BuildingBlocks.Common/Results/Error.cs#L7).

```csharp
public class Result
{
    // ...
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
    // ...
    public static implicit operator Result(Error error) => Failure(error);
}
```

```csharp
public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    // ...
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    // ...
}
```

Gracias a la conversión implícita, en el dominio se escribe simplemente `return OrderErrors.NoItems;`.

**Repreguntas típicas:** ¿Y entonces por qué `Order.Place` lanza `ArgumentException` a veces? (Son **guard
clauses** contra errores de programación, como un `customerId` vacío que debería venir del token. Un usuario no
puede provocarlos; las reglas que sí puede violar devuelven `Result`.)

### 2.3 ¿Cómo se envía un e-mail?

**Respuesta corta:** lo envía la **Azure Function de Notifications** cuando recibe `OrderConfirmed` u
`OrderRejected`. Primero **guarda la notificación** (con su fila de inbox, en una transacción) y **recién después**
manda el e-mail con **MailKit** por SMTP. Si el e-mail falla, se registra en la notificación y **no** se reintenta
el mensaje.

**El recorrido completo:**

1. `OrderEventsFunction` es el trigger de Service Bus y delega en `OrderEventHandler`.
2. `OrderEventHandler` hace el check del inbox, guarda la notificación y la fila del inbox, y llama a `IEmailSender`.
3. `IEmailSender` se resuelve a `SmtpEmailSender` (MailKit) o a `NoOpEmailSender` (Null Object) según
   `Email:Enabled`. La elección se hace **una sola vez al arrancar**.

**Dónde está en el código:**

La elección del remitente ([EmailRegistration.cs](../src/Functions/Notifications/Email/EmailRegistration.cs#L13)):

```csharp
public static IHostApplicationBuilder AddEmailSending(this IHostApplicationBuilder builder)
{
    // ...
    builder.Services.AddOptions<EmailOptions>()
        .Bind(section)
        .ValidateDataAnnotations()
        .Validate(options => options.Timeout > TimeSpan.Zero, "Email:Timeout must be positive.")
        .ValidateOnStart();

    if (section.GetValue("Enabled", defaultValue: true))
    {
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
    }
    else
    {
        builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();
    }

    return builder;
}
```

El envío con MailKit ([SmtpEmailSender.cs](../src/Functions/Notifications/Email/SmtpEmailSender.cs#L24)):

```csharp
var mail = new MimeMessage
{
    Subject = message.Subject,
    Body = new TextPart(TextFormat.Plain) { Text = message.Body },
};

mail.From.Add(new MailboxAddress(_options.FromDisplayName, _options.From));
mail.To.Add(MailboxAddress.Parse(message.To));
// ...
await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, timeout.Token);

// Mailpit accepts anonymous SMTP; Gmail needs the address and an App Password.
if (!string.IsNullOrWhiteSpace(_options.Username))
{
    await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, timeout.Token);
}

await client.SendAsync(mail, timeout.Token);
```

Por qué el e-mail va **después** del commit ([OrderEventHandler.cs](../src/Functions/Notifications/Messaging/OrderEventHandler.cs#L116)):

```csharp
/// <summary>
/// Best effort by design: a failure is written on the notification and logged, never rethrown. Retrying the
/// message would find the inbox row and skip it, so throwing here would only dead-letter an order outcome
/// that was already recorded correctly.
/// </summary>
private async Task DeliverEmailAsync(Notification notification, CancellationToken cancellationToken)
{
    try
    {
        var delivery = await emailSender.SendAsync(
            new EmailMessage(notification.CustomerEmail, notification.Title, notification.Body),
            cancellationToken);
        // ...
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        notification.MarkEmailFailed(ex.Message);
        LogEmailFailed(logger, ex, notification.CustomerEmail, notification.OrderId);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
}
```

**Qué hay que configurar** (sección `Email`, [EmailOptions.cs](../src/Functions/Notifications/Email/EmailOptions.cs#L10)):

| Setting | Por defecto (Mailpit) | Gmail |
|---|---|---|
| `Email:Enabled` | `true` | `true` (`false` activa el Null Object) |
| `Email:Host` | `localhost` (Aspire lo inyecta) | `smtp.gmail.com` |
| `Email:Port` | `1025` | `587` |
| `Email:UseStartTls` | `false` | `true` |
| `Email:Username` / `Email:Password` | vacíos | tu dirección + **App Password** (requiere verificación en 2 pasos) |
| `Email:From` | `no-reply@microservices-demo.local` | tu dirección |
| `DemoUsers:bar:Email` (en el Gateway) | `bar@example.com` | la casilla que recibe los e-mails |

- **Con Aspire:** `Email:Host` y `Email:Port` los inyecta el AppHost apuntando a Mailpit; la contraseña de Gmail va
  en **user-secrets**, nunca en un archivo del repositorio.
- **Con docker-compose:** variables `EMAIL_*` en un `.env` que git ignora ([.env.example](../.env.example#L43)).
- Los e-mails capturados se ven en `http://localhost:8025`.

**Repreguntas típicas:** ¿Qué pasa si el proceso muere entre guardar y enviar? (La notificación queda con
`EmailStatus.Pending` y no se envía; es un canal *best effort*. Para garantizarlo habría que agregar un outbox de
e-mails.) ¿Por qué el `IEmailSender` devuelve `EmailDelivery` y no `Task` a secas? (Para que el Null Object no pueda
dejar la notificación diciendo "enviado".)

### 2.4 ¿Por qué Mapperly? ¿Cuándo conviene el mapeo manual?

**Respuesta corta:** **Mapperly** es un **source generator**: genera el código de mapeo **al compilar**. Si
agregas o renombras un campo del DTO y no lo mapeas, **no compila**. No usa reflection, no necesita DI y el código
generado se puede leer y depurar. Además genera **proyecciones `IQueryable`**, que EF Core traduce a SQL con la
misma definición del mapeo en memoria.

**Historia:** primero se usó **Mapster** (ADR 0005). Se reemplazó por Mapperly (ADR 0006) porque los errores de
Mapster aparecían en tests o en runtime, no en el build. **AutoMapper** se descartó por su licencia comercial desde
la v15.

**Dónde está en el código:** [OrderMapper.cs](../src/Services/Ordering/Ordering.Application/Orders/OrderMapper.cs#L20).

```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class OrderMapper
{
    [MapProperty("Subtotal.Amount", nameof(OrderResponse.Subtotal))]
    [MapProperty("Discount.Amount", nameof(OrderResponse.Discount))]
    [MapProperty("Total.Amount", nameof(OrderResponse.Total))]
    [MapProperty("Total.Currency", nameof(OrderResponse.Currency))]
    [MapProperty(nameof(Order.Items), nameof(OrderResponse.Items), Use = nameof(MapItemsBySku))]
    public static partial OrderResponse ToResponse(this Order order);
    // ...
    /// <summary>Read side: translated by EF Core into the SELECT list, no aggregate is materialized.</summary>
    public static partial IQueryable<OrderResponse> ProjectToResponse(this IQueryable<Order> query);
    // ...
    // LineTotal is computed in the domain and not stored; recomputed here so it also works in SQL.
    // Rounded so PostgreSQL returns cents (numeric multiplication would widen the scale).
    private static decimal LineTotal(OrderItem item) => Math.Round(item.UnitPrice.Amount * item.Quantity.Value, 2);
}
```

**Mapeo manual:** Catalog y Notifications mapean **a mano a propósito**, como referencia. Con uno o dos DTOs, una
expresión escrita a mano es más simple que una librería
([ProductResponse.cs](../src/Services/Catalog/Catalog.Api/Features/Products/ProductResponse.cs#L15)):

```csharp
/// <summary>Translated to SQL by EF Core, so read queries only select the columns they return.</summary>
public static readonly Expression<Func<Product, ProductResponse>> Projection = product =>
    new ProductResponse(
        product.Id,
        product.Sku,
        product.Name,
        product.Style,
        product.VolumeMl,
        product.PackSize,
        product.Price);

private static readonly Func<Product, ProductResponse> _fromProduct = Projection.Compile();

public static ProductResponse FromProduct(Product product) => _fromProduct(product);
```

**Cuándo elegir cada uno:**

| Situación | Elección |
|---|---|
| Pocos DTOs, casi iguales a la entidad | A mano (expresión + factory) |
| DTOs anidados (pedido + ítems), en memoria y en SQL | Mapperly: una definición para los dos caminos |
| El mapeo tiene lógica que no entra en una expresión | A mano para ese caso |
| Contratos públicos entre servicios (eventos) | A mano siempre: cada campo que cruza la frontera se escribe explícitamente (`IntegrationEventMapper`) |

**Regla del proyecto:** los mapeos van **solo de entidad a DTO**. Un agregado nunca se crea con un mapper, porque
se saltaría su factory method y sus invariantes.

### 2.5 ¿Por qué usaste cada librería?

**Respuesta corta:** cada dependencia tiene que justificar su lugar; donde el patrón es chico y es lo que la demo
quiere mostrar (outbox, decorators), se escribió a mano.

| Librería | Para qué | Por qué esta |
|---|---|---|
| **EF Core + Npgsql** | Persistencia | Estándar de .NET; migraciones, LINQ, proyecciones, interceptores |
| **FluentValidation** | Validar requests y comandos | Reglas en clases testeables y reutilizables |
| **Mapperly** | Entidad → DTO | Generado al compilar, errores en el build (ADR 0006) |
| **Azure.Messaging.ServiceBus** | Mensajería | SDK oficial, MIT; sin framework para que outbox e inbox se vean (ADR 0001) |
| **Polly v8** (`Microsoft.Extensions.Http.Resilience`) | Retry, circuit breaker, timeout | El estándar de .NET, integrado con `HttpClientFactory` |
| **YARP** | API Gateway | Proxy de Microsoft, configurable, en .NET (ADR 0004) |
| **MailKit** | SMTP | Microsoft recomienda MailKit en lugar de `System.Net.Mail.SmtpClient` para código nuevo |
| **.NET Aspire** | Orquestación local y observabilidad | Un comando levanta todo, con dashboard OpenTelemetry |
| **Scalar** | UI de la referencia OpenAPI | Reemplazo moderno de Swagger UI |
| **xUnit v3, Shouldly, NSubstitute** | Tests | Estándar, asserts legibles, mocks simples |
| **Testcontainers** | Tests de integración | PostgreSQL real en Docker, descartable |
| **NetArchTest** | Tests de arquitectura | Verifica las dependencias entre capas en el build |

Descartadas: **MediatR** y **AutoMapper** (licencias comerciales; ver [3.9](#39-por-qué-no-hay-mediatr)),
**MassTransit** (licencia comercial desde la v9 y oculta los patrones).

**Dónde está en el código:** [Directory.Packages.props](../Directory.Packages.props#L54).

```xml
<!-- Gateway and authentication -->
<ItemGroup>
  <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.12" />
  <PackageVersion Include="Yarp.ReverseProxy" Version="2.3.0" />
  <PackageVersion Include="Microsoft.Extensions.ServiceDiscovery.Yarp" Version="10.10.0" />
</ItemGroup>

<!-- Application layer: object mapping and abstractions -->
<ItemGroup>
  <PackageVersion Include="Riok.Mapperly" Version="4.3.1" />
  <!-- ... -->
</ItemGroup>
```

### 2.6 ¿Qué es el Options pattern?

**Respuesta corta:** la configuración (`appsettings.json`, variables de entorno, user-secrets) se enlaza a una
**clase tipada** y se inyecta como `IOptions<T>`. Con `ValidateOnStart()` una configuración inválida **impide que
el servicio arranque**, en lugar de fallar en el primer request.

**Dónde está en el código:** [PricingOptions.cs](../src/Services/Ordering/Ordering.Application/Pricing/PricingOptions.cs#L6)
y su registro ([DependencyInjection.cs](../src/Services/Ordering/Ordering.Application/DependencyInjection.cs#L36)).

```csharp
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    /// <summary>ISO 4217 code applied to Catalog prices, which carry no currency.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Volume discount tiers. Empty means no discount (<see cref="NoDiscountPolicy"/>).</summary>
    public List<VolumeDiscountTier> VolumeDiscountTiers { get; set; } = [];
}
```

```csharp
services.AddOptions<PricingOptions>()
    .Validate(options => Money.Create(0, options.Currency).IsSuccess, "Pricing:Currency must be an ISO 4217 code.")
    .ValidateOnStart();
```

En `appsettings.json` de Ordering los tramos son 20 packs → 5 % y 50 packs → 10 %.

### 2.7 ¿Cómo funciona la autorización? ¿Qué es la *fallback policy*?

**Respuesta corta:** todos los endpoints exigen un usuario autenticado **por defecto** (fallback policy). Lo
público tiene que declararse con `AllowAnonymous` y lo de back-office con `RequireAdmin()` o `[RequireAdmin]`. Si
alguien olvida proteger un endpoint, queda **cerrado**, no abierto.

**Dónde está en el código:** [JwtAuthenticationExtensions.cs](../src/BuildingBlocks/BuildingBlocks.Web/Authentication/JwtAuthenticationExtensions.cs#L56).

```csharp
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(Roles.Admin));
```

Y la identidad del cliente sale **siempre** de los claims del token, nunca del body ni de un header
([CurrentCustomer.cs](../src/Services/Ordering/Ordering.Api/Customers/CurrentCustomer.cs#L14)):

```csharp
[FromAccessToken]
public sealed record CurrentCustomer(string Id, string Email)
{
    public static CurrentCustomer FromPrincipal(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(JwtClaimNames.Subject);
        var email = user.FindFirstValue(JwtClaimNames.Email);
        // ...
```

**Repreguntas típicas:** ¿Qué devuelve pedir el pedido de otro cliente? (404, no 403: la consulta filtra por
`customerId`, así que para ese cliente el pedido no existe y no se revela que existe.) ¿Diferencia entre 401 y 403?
(401: no sé quién eres; 403: sé quién eres y no tienes permiso.)

### 2.8 ¿Qué son CORS y el rate limiting, y por qué están en el Gateway?

**Respuesta corta:**

- **CORS:** el navegador solo deja que una página de `localhost:5173` llame a otro origen (`localhost:5100`) si ese
  origen lo permite. El Gateway permite **una lista fija de orígenes**, nunca `*`.
- **Rate limiting:** limita cuántos requests puede hacer cada cliente en una ventana de tiempo: 100 cada 10 s en
  `/api/*` y **10 por minuto** en `/auth/token`, que acepta contraseñas. Se cuenta por usuario (`sub`) o por IP si es
  anónimo.

Están en el Gateway porque son preocupaciones **del borde**: el navegador solo habla con el Gateway.

**Dónde está en el código:** [RateLimitingExtensions.cs](../src/Gateway/RateLimiting/RateLimitingExtensions.cs#L52)
y [CorsExtensions.cs](../src/Gateway/Cors/CorsExtensions.cs#L23).

```csharp
private static RateLimitPartition<string> FixedWindow(HttpContext context, RateLimitWindow window) =>
    RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = window.PermitLimit,
            Window = window.Window,
            QueueLimit = 0,
        });

private static string PartitionKey(HttpContext context) =>
    context.User.FindFirst(JwtClaimNames.Subject)?.Value
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";
```

```csharp
return services.AddCors(cors => cors.AddPolicy(CorsPolicies.Web, policy => policy
    .WithOrigins(options.AllowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // The web app follows the Location header of a 202 Accepted to poll the new order.
    .WithExposedHeaders("Location")));
```

**Repreguntas típicas:** ¿Por qué CORS va antes que la autenticación en el pipeline? (El *preflight* `OPTIONS` del
navegador no lleva token; si la autorización corriera primero lo rechazaría.) ¿El rate limiter escala a varias
instancias? (No: la ventana vive en memoria, por instancia; en producción iría a Redis o al borde, como Front Door.)

### 2.9 ¿Qué es Vertical Slice Architecture y cómo se ve en Catalog?

**Respuesta corta:** en lugar de organizar por capas técnicas (controllers, services, repositories), se organiza por
**funcionalidad**: cada carpeta tiene todo lo de un caso de uso (endpoint, request, handler, validator). Cambiar
"crear producto" es tocar una sola carpeta.

**Dónde está en el código:** [src/Services/Catalog/Catalog.Api/Features/Products](../src/Services/Catalog/Catalog.Api/Features/Products).

```
Features/Products/
├─ CreateProduct/     CreateProductEndpoint, CreateProductHandler, CreateProductRequest, CreateProductValidator
├─ GetProductById/
├─ GetProducts/
├─ UpdateProduct/
├─ ProductResponse.cs
└─ ProductRules.cs     reglas compartidas por más de un slice
```

El handler usa el `DbContext` directamente, sin repositorio
([CreateProductHandler.cs](../src/Services/Catalog/Catalog.Api/Features/Products/CreateProduct/CreateProductHandler.cs#L9)):

```csharp
internal sealed class CreateProductHandler(CatalogDbContext dbContext)
{
    public async Task<Result<ProductResponse>> HandleAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = Product.NormalizeSku(request.Sku);

        if (await dbContext.Products.AnyAsync(product => product.Sku == sku, cancellationToken))
        {
            return ProductErrors.SkuAlreadyExists(sku);
        }
        // ...
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two concurrent requests passed the check above; the unique index decides.
            return ProductErrors.SkuAlreadyExists(sku);
        }

        return ProductResponse.FromProduct(product);
    }
}
```

**Repreguntas típicas:** ¿Por qué el `catch` si ya chequeaste que no existe? (Dos requests concurrentes pueden pasar
el chequeo a la vez; el índice único de la base es el que decide, y el `catch` lo traduce a un 409.)

### 2.10 ¿Qué es la capa de servicio de Inventory y qué significa "Update" vs "Stage"?

**Respuesta corta:** Inventory sigue el estilo más común en .NET: **controller → interfaz de servicio →
implementación** (ADR 0008). Todo lo que toca stock pasa por `IStockService`, tanto desde HTTP como desde el
consumidor de `OrderPlaced`. Los nombres dicen **quién guarda**:

- `Update...` **guarda** (`SaveChangesAsync`): un request HTTP es su propia unidad de trabajo.
- `Stage...` **no guarda**: deja los cambios en el `DbContext` y el pipeline del consumidor los guarda **junto con**
  el evento del outbox y la fila del inbox, en una sola transacción.

**Dónde está en el código:** [IStockService.cs](../src/Services/Inventory/Inventory.Api/Services/IStockService.cs#L17)
y [StockService.cs](../src/Services/Inventory/Inventory.Api/Services/StockService.cs#L54).

```csharp
public interface IStockService
{
    Task<IReadOnlyList<StockResponse>> GetStockAsync(IReadOnlyCollection<string>? skus, CancellationToken cancellationToken);

    Task<Result<StockResponse>> UpdateQuantityAvailableAsync(string sku, UpdateStockRequest request, CancellationToken cancellationToken);

    Task<ReservationOutcome> StageReservationAsync(IReadOnlyCollection<ReservationLine> lines, CancellationToken cancellationToken);
}
```

```csharp
public async Task<ReservationOutcome> StageReservationAsync(IReadOnlyCollection<ReservationLine> lines, CancellationToken cancellationToken)
{
    var skus = NormalizeSkus(lines.Select(line => line.Sku));

    // Tracked (not AsNoTracking): these rows are the ones the caller will save.
    var stockItems = await dbContext.StockItems
        .Where(stockItem => skus.Contains(stockItem.Sku))
        .ToListAsync(cancellationToken);

    return StockReservation.Reserve(stockItems, lines, timeProvider.GetUtcNow());
}
```

**Repreguntas típicas:** ¿Qué pasaría si `StageReservationAsync` guardara? (El stock quedaría reservado sin su
evento de respuesta en el outbox si algo falla después: un paso de la saga perdido.) ¿Una interfaz con una sola
implementación no es ceremonia? (Aquí se justifica: es el contrato que comparten los dos puntos de entrada y la
costura para mockear en los tests del controller.)

### 2.11 ¿Cómo se testea? ¿Cuándo mockear y cuándo no?

**Respuesta corta:** se mockea **lo que el código controla** y se usa **infraestructura real donde un mock
mentiría**. El controller de Inventory se prueba mockeando `IStockService` con NSubstitute; el servicio, que habla
con la base, se prueba contra un **PostgreSQL real** con Testcontainers. **La base de datos nunca se mockea**: un
fake acepta cualquier cosa que el test espere, PostgreSQL no (índices únicos, `xmin`, traducción de LINQ a SQL).

**Dónde está en el código:**

Unitario con NSubstitute ([StockControllerTests.cs](../tests/Inventory.UnitTests/Controllers/StockControllerTests.cs#L24)):

```csharp
private readonly IStockService _stockService = Substitute.For<IStockService>();

[Fact]
public async Task GetStock_SkuFilter_PassesSkusToServiceAndReturnsOk()
{
    string[] skus = ["golden-lager-350"];
    _stockService.GetStockAsync(skus, Arg.Any<CancellationToken>()).Returns([_goldenLager]);

    var result = await CreateController().GetStock(new GetStockRequest(skus), TestContext.Current.CancellationToken);

    var ok = result.Result.ShouldBeOfType<OkObjectResult>();
    ok.Value.ShouldBe(new[] { _goldenLager });
    await _stockService.Received(1).GetStockAsync(skus, TestContext.Current.CancellationToken);
}
```

Integración con Testcontainers ([OrderingApiFactory.cs](../tests/Ordering.IntegrationTests/Infrastructure/OrderingApiFactory.cs#L16)):
la API real contra un PostgreSQL real; solo se falsean los bordes (el broker y Catalog).

```csharp
public sealed class OrderingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    // ...
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:orderingdb", _postgres.GetConnectionString());
        // ...
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus>(EventBus);

            services.RemoveAll<ICatalogClient>();
            services.AddSingleton<ICatalogClient>(CatalogClient);
        });
    }
}
```

Un test de integración clave verifica que el pedido y su fila del outbox se escriben **atómicamente**
(`PlaceOrder_KnownProducts_Returns202AndSavesOrderWithOutboxMessageAtomically`, en
[PlaceOrderTests.cs](../tests/Ordering.IntegrationTests/Orders/PlaceOrderTests.cs#L20)).

### 2.12 ¿Qué es `AsNoTracking` y por qué las lecturas son proyecciones?

**Respuesta corta:** EF Core, por defecto, **rastrea** las entidades que carga para detectar cambios al guardar.
En una lectura eso es trabajo inútil: `AsNoTracking()` lo apaga. Y al **proyectar** con `Select` (o
`ProjectToResponse()`), EF Core genera un `SELECT` solo con las columnas del DTO, sin materializar entidades.

**Dónde está en el código:** [GetOrdersQueryHandler.cs](../src/Services/Ordering/Ordering.Infrastructure/Queries/GetOrdersQueryHandler.cs#L15).

```csharp
public async Task<Result<IReadOnlyList<OrderSummaryResponse>>> HandleAsync(GetOrdersQuery query, CancellationToken cancellationToken)
{
    var orders = await dbContext.Orders
        .AsNoTracking()
        .Where(order => order.CustomerId == query.CustomerId)
        .OrderByDescending(order => order.PlacedOnUtc)
        .Take(MaxOrders)
        .ProjectToSummary()
        .ToListAsync(cancellationToken);

    return orders;
}
```

Contraejemplo: cuando **sí** vas a modificar, cargas con tracking, como en `StageReservationAsync` (ver [2.10](#210-qué-es-la-capa-de-servicio-de-inventory-y-qué-significa-update-vs-stage)).

### 2.13 ¿Qué es Central Package Management y por qué "warnings as errors"?

**Respuesta corta:** **Central Package Management** declara la versión de cada NuGet **una sola vez**
(`Directory.Packages.props`); los proyectos referencian el paquete sin versión. Así no hay dos proyectos con
versiones distintas de la misma librería. **Warnings as errors** + **code style en el build** hace que un warning
o un problema de formato rompa el build, local y en CI, en lugar de acumularse.

**Dónde está en el código:** [Directory.Build.props](../Directory.Build.props#L3).

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <LangVersion>latest</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <AnalysisLevel>latest</AnalysisLevel>
  <!-- ... -->
</PropertyGroup>
```

Esto además potencia a Mapperly: un miembro sin mapear es un warning, y el warning es un error.

---
## Nivel 3 — Senior

### 3.1 ¿Qué patrones de diseño hay en el proyecto y dónde se ven?

**Respuesta corta:** hay patrones **GoF** (Strategy, Decorator, Adapter, Null Object, Factory Method), de **DDD**
(Aggregate Root, Value Object, Domain Events), de **persistencia** (Repository, Unit of Work), de **mensajería**
(Transactional Outbox, Idempotent Consumer, Pub/Sub, Saga) y otros (Result, Options, Test Data Builder, CQRS). La
idea es que cada patrón **resuelva un problema concreto** del proyecto, no que esté para decorar.

Mapa rápido; abajo, cada patrón explicado con su código:

| Patrón | Problema que resuelve aquí | Dónde |
|---|---|---|
| Strategy | Cambiar la regla de descuento sin tocar el pedido | `IDiscountPolicy` |
| Decorator | Logging y validación en todos los comandos sin repetir código | `LoggingDecorator`, `ValidationDecorator` |
| Adapter | Que el negocio no dependa de Service Bus ni de SMTP | `IEventBus`, `IEmailSender`, `ICatalogClient` |
| Null Object | Apagar e-mails o descuentos sin `if` en el código | `NoOpEmailSender`, `NoDiscountPolicy` |
| Factory Method | Que un pedido inválido no pueda existir | `Order.Place(...)` |
| Value Object | Dinero, SKU y cantidad siempre válidos | `Money`, `Sku`, `Quantity` |
| Aggregate Root + Domain Events | Un único punto de cambio y un registro de lo que pasó | `Order`, `AggregateRoot<T>` |
| State Machine | Transiciones de estado inválidas imposibles | `Order._allowedTransitions` |
| Repository + Unit of Work | Cargar el agregado entero y guardar todo en una transacción | `IOrderRepository`, `IUnitOfWork` |
| CQRS liviano | Escrituras por el agregado, lecturas por proyección | Commands en Application, queries en Infrastructure |
| Result | Fallas esperadas como valores | [2.2](#22-qué-es-el-result-pattern-y-por-qué-no-usar-excepciones) |
| Options | Configuración tipada y validada al arrancar | [2.6](#26-qué-es-el-options-pattern) |
| Test Data Builder | Tests que solo dicen lo que les importa | `OrderBuilder` |
| Transactional Outbox / Inbox | No perder ni duplicar eventos | [3.3](#33-qué-es-el-transactional-outbox-y-qué-problema-resuelve), [3.4](#34-cómo-se-logra-que-un-consumidor-sea-idempotente-inbox) |
| Saga por coreografía | Consistencia entre servicios sin transacción distribuida | [4.4](#44-coreografía-u-orquestación-por-qué-una-saga-por-coreografía) |
| API Gateway | Punto de entrada único | [4.3](#43-por-qué-yarp-y-qué-hace) |

**Un solo constructor, cinco patrones:** el handler de hacer un pedido recibe un Adapter (`ICatalogClient`), un
Repository, un Unit of Work, una Strategy y unas Options
([PlaceOrderCommandHandler.cs](../src/Services/Ordering/Ordering.Application/Orders/PlaceOrder/PlaceOrderCommandHandler.cs#L18)).
Y al registrarse queda envuelto por dos Decorators.

```csharp
internal sealed class PlaceOrderCommandHandler(
    ICatalogClient catalogClient,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    IDiscountPolicy discountPolicy,
    IOptions<PricingOptions> pricingOptions,
    TimeProvider timeProvider) : ICommandHandler<PlaceOrderCommand, OrderResponse>
```

#### 3.1.1 Strategy — `IDiscountPolicy`

**Qué es:** encapsular una familia de algoritmos intercambiables detrás de una interfaz; quien la usa no sabe cuál
le tocó. **Para qué sirve aquí:** el pedido pide "el descuento" sin saber qué promoción está vigente. Agregar una
promoción nueva es una clase nueva, no un `if` más en `Order`.

[IDiscountPolicy.cs](../src/Services/Ordering/Ordering.Domain/Discounts/IDiscountPolicy.cs#L9) y
[VolumeDiscountPolicy.cs](../src/Services/Ordering/Ordering.Domain/Discounts/VolumeDiscountPolicy.cs#L42):

```csharp
public interface IDiscountPolicy
{
    Money CalculateDiscount(Money subtotal, int totalQuantity);
}
```

```csharp
public Money CalculateDiscount(Money subtotal, int totalQuantity)
{
    ArgumentNullException.ThrowIfNull(subtotal);

    var tier = _tiers.FirstOrDefault(tier => totalQuantity >= tier.MinimumQuantity);

    return tier is null
        ? Money.Zero(subtotal.Currency)
        : subtotal.ApplyRate(tier.Rate);
}
```

La estrategia se **elige por configuración** al registrar
([DependencyInjection.cs](../src/Services/Ordering/Ordering.Application/DependencyInjection.cs#L42)):

```csharp
// Strategy selected from configuration; the constructor of VolumeDiscountPolicy rejects invalid tiers.
services.AddSingleton<IDiscountPolicy>(serviceProvider =>
{
    var tiers = serviceProvider.GetRequiredService<IOptions<PricingOptions>>().Value.VolumeDiscountTiers;
    return tiers.Count == 0 ? NoDiscountPolicy.Instance : new VolumeDiscountPolicy(tiers);
});
```

#### 3.1.2 Decorator — `LoggingDecorator` y `ValidationDecorator`

**Qué es:** envolver un objeto con otro que implementa **la misma interfaz** y agrega comportamiento antes o
después de delegar. **Para qué sirve aquí:** cada comando pasa por **logging → validación → handler** sin que el
handler tenga una línea de logging ni de validación. Es lo que MediatR llama *pipeline behaviors*, escrito a mano.

[ValidationDecorator.cs](../src/Services/Ordering/Ordering.Application/Decorators/ValidationDecorator.cs#L13):

```csharp
internal sealed class ValidationDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(validation.Errors);
        }

        if (failures.Count == 0)
        {
            return await inner.HandleAsync(command, cancellationToken);
        }
        // ...
        return new ValidationError(errors);
    }
}
```

El armado de la cadena ([DependencyInjection.cs](../src/Services/Ordering/Ordering.Application/DependencyInjection.cs#L54)):

```csharp
private static void AddCommandHandler<TCommand, TResponse, THandler>(this IServiceCollection services)
    where TCommand : ICommand<TResponse>
    where THandler : class, ICommandHandler<TCommand, TResponse>
{
    services.AddScoped<THandler>();
    services.AddScoped<ICommandHandler<TCommand, TResponse>>(serviceProvider =>
        new LoggingDecorator<TCommand, TResponse>(
            new ValidationDecorator<TCommand, TResponse>(
                serviceProvider.GetRequiredService<THandler>(),
                serviceProvider.GetServices<IValidator<TCommand>>()),
            serviceProvider.GetRequiredService<ILogger<LoggingDecorator<TCommand, TResponse>>>()));
}
```

**Repregunta típica:** ¿Por qué la validación está también en Application si ya hay un filtro HTTP? (El decorator
protege el comando **venga de donde venga**: HTTP, un consumidor de mensajes o un test.)

#### 3.1.3 Adapter — `IEventBus`, `IEmailSender`, `ICatalogClient`

**Qué es:** traducir una interfaz que tu código entiende a otra que ofrece una librería externa. **Para qué sirve
aquí:** el negocio conoce "publicar un mensaje", no Azure Service Bus. Cambiar a RabbitMQ sería escribir otro
adapter, sin tocar los servicios.

[IEventBus.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/IEventBus.cs#L3) y
[AzureServiceBusEventBus.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/ServiceBus/AzureServiceBusEventBus.cs#L13):

```csharp
/// <summary>
/// Transport abstraction (Adapter pattern). Only the outbox processor publishes through it, so business code
/// never talks to the broker directly. Swapping Azure Service Bus for RabbitMQ or Kafka means a new adapter.
/// </summary>
public interface IEventBus
{
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}
```

```csharp
public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
{
    // ...
    var serviceBusMessage = new ServiceBusMessage(BinaryData.FromString(message.Payload))
    {
        MessageId = message.MessageId.ToString(),
        Subject = message.Subject,
        CorrelationId = message.CorrelationId,
        ContentType = "application/json",
    };
    // ...
    var sender = _senders.GetOrAdd(message.Topic, client.CreateSender);
    await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
}
```

`CatalogClient` es el mismo patrón para HTTP: traduce excepciones de red y de Polly a un `Result` con
`Catalog.Unavailable` ([CatalogClient.cs](../src/Services/Ordering/Ordering.Infrastructure/Catalog/CatalogClient.cs#L46)).

#### 3.1.4 Null Object — `NoOpEmailSender`, `NoDiscountPolicy`

**Qué es:** una implementación que "no hace nada" pero cumple la interfaz, para no llenar el código de
`if (x != null)` o `if (enabled)`. **Para qué sirve aquí:** apagar los e-mails es un cambio de configuración, no un
`if` en el handler.

[NoOpEmailSender.cs](../src/Functions/Notifications/Email/NoOpEmailSender.cs#L9):

```csharp
internal sealed partial class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task<EmailDelivery> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        LogSkipped(logger, message.To, message.Subject);
        return Task.FromResult(EmailDelivery.Skipped);
    }
    // ...
}
```

Un test garantiza que la elección se hace al componer
([EmailRegistrationTests.cs](../tests/Notifications.UnitTests/Email/EmailRegistrationTests.cs#L15)):

```csharp
[Fact]
public void AddEmailSending_EmailDisabled_ResolvesTheNullObject()
{
    using var host = BuildHost(("Email:Enabled", "false"));

    host.Services.GetRequiredService<IEmailSender>().ShouldBeOfType<NoOpEmailSender>();
}
```

#### 3.1.5 Factory Method y Guard Clauses — `Order.Place(...)`

**Qué es:** un método estático que es **la única forma** de crear el objeto; el constructor es privado. **Para qué
sirve aquí:** garantiza que no existe un `Order` inválido. Las **guard clauses** lanzan excepción ante errores de
programación y las reglas de negocio devuelven `Result`.

[Order.cs](../src/Services/Ordering/Ordering.Domain/Orders/Order.cs#L73):

```csharp
/// <summary>
/// Factory method: the only way to create an order, so an <see cref="Order"/> instance is always valid.
/// </summary>
public static Result<Order> Place(
    string customerId,
    string customerEmail,
    IReadOnlyCollection<OrderLine> lines,
    IDiscountPolicy discountPolicy,
    DateTimeOffset placedOnUtc)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
    ArgumentException.ThrowIfNullOrWhiteSpace(customerEmail);
    ArgumentNullException.ThrowIfNull(lines);
    ArgumentNullException.ThrowIfNull(discountPolicy);
    // ...
    var validation = ValidateLines(lines);
    if (validation.IsFailure)
    {
        return validation.Error;
    }
    // ...
```

Nota: la hora se **recibe** (`placedOnUtc`); el dominio nunca lee el reloj, lo que lo hace determinista en tests.

#### 3.1.6 Value Object — `Money`, `Sku`, `Quantity`

**Qué es:** un objeto definido por su **valor**, no por una identidad; inmutable y siempre válido. Dos `Money` de
"10.00 USD" son iguales. **Para qué sirve aquí:** impide cosas como sumar dólares con euros, montos negativos o un
SKU mal formado, y concentra esas reglas en un solo lugar.

[Money.cs](../src/Services/Ordering/Ordering.Domain/ValueObjects/Money.cs#L9):

```csharp
public sealed record Money
{
    // ...
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            return MoneyErrors.NegativeAmount;
        }

        if (decimal.Round(amount, Scale) != amount)
        {
            return MoneyErrors.InvalidScale;
        }
        // ...
        return new Money(amount, normalizedCurrency);
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }
    // ...
}
```

`sealed record` da igualdad por valor e inmutabilidad; el constructor privado obliga a pasar por `Create`. En EF
Core se mapean como **complex types**, así que viven en las columnas de la tabla `orders` sin joins
([OrderConfiguration.cs](../src/Services/Ordering/Ordering.Infrastructure/Persistence/Configurations/OrderConfiguration.cs#L25)).

#### 3.1.7 Aggregate Root, Domain Events y State Machine — `Order`

**Qué es:**

- **Aggregate Root:** la entidad "raíz" por la que pasa **todo cambio** del grupo (el pedido y sus ítems). Es la
  frontera de consistencia.
- **Domain Event:** un registro de "algo que pasó" en el dominio (`OrderPlaced`), que el agregado acumula.
- **State Machine:** transiciones permitidas explícitas; una transición inválida es un error, no un `if` olvidado.

**Para qué sirve aquí:** los ítems solo se modifican a través de `Order`; los eventos acumulados se transforman en
filas del outbox al guardar; un evento tardío o duplicado que intenta confirmar un pedido ya rechazado devuelve
`Conflict` en lugar de corromper el estado.

[AggregateRoot.cs](../src/Services/Ordering/Ordering.Domain/Abstractions/AggregateRoot.cs#L7) y
[Order.cs](../src/Services/Ordering/Ordering.Domain/Orders/Order.cs#L118):

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];
    // ...
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
```

```csharp
/// <summary>Inventory reserved the stock.</summary>
public Result Confirm(DateTimeOffset confirmedOnUtc)
{
    var transition = TransitionTo(OrderStatus.Confirmed, confirmedOnUtc);
    if (transition.IsFailure)
    {
        return transition;
    }

    Raise(new OrderConfirmedDomainEvent(Id, CustomerId, CustomerEmail, Total, confirmedOnUtc));
    return Result.Success();
}
// ...
private Result TransitionTo(OrderStatus next, DateTimeOffset occurredOnUtc)
{
    if (!_allowedTransitions[Status].Contains(next))
    {
        return OrderErrors.InvalidStatusTransition(Id, Status, next);
    }

    Status = next;
    CompletedOnUtc = occurredOnUtc;
    return Result.Success();
}
```

#### 3.1.8 Repository + Unit of Work — `IOrderRepository`, `IUnitOfWork`

**Qué es:** el **Repository** es una colección de agregados en memoria (agregar, obtener por id) que oculta la
persistencia; el **Unit of Work** junta todos los cambios y los confirma en una transacción. **Para qué sirve
aquí:** Application no conoce EF Core; el repositorio siempre carga el agregado **entero** (pedido + ítems) para que
sus invariantes se verifiquen en memoria. El `DbContext` es el Unit of Work.

[OrderRepository.cs](../src/Services/Ordering/Ordering.Infrastructure/Persistence/OrderRepository.cs#L6) y
[OrderingDbContext.cs](../src/Services/Ordering/Ordering.Infrastructure/Persistence/OrderingDbContext.cs#L12):

```csharp
internal sealed class OrderRepository(OrderingDbContext dbContext) : IOrderRepository
{
    // The aggregate is always loaded whole (root + items) so its invariants can be checked in memory.
    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public void Add(Order order) => dbContext.Orders.Add(order);
}
```

```csharp
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options), IUnitOfWork
```

**Repregunta típica:** ¿Por qué Catalog e Inventory **no** tienen repositorio? (El `DbContext` ya es un repositorio
y un unit of work. Solo se agrega la capa donde hay un agregado que proteger y una capa Application que no debe
conocer EF Core.)

#### 3.1.9 CQRS liviano

**Qué es:** separar el modelo de **escritura** (comandos que cambian estado) del de **lectura** (consultas). No
implica dos bases de datos. **Para qué sirve aquí:** los comandos pasan por el agregado y sus reglas; las consultas
proyectan directo a DTOs y **nunca cargan el agregado**. Los query handlers viven en Infrastructure porque son
consultas de EF Core.

Comando: [PlaceOrderCommandHandler.cs](../src/Services/Ordering/Ordering.Application/Orders/PlaceOrder/PlaceOrderCommandHandler.cs#L56).
Consulta: [GetOrdersQueryHandler.cs](../src/Services/Ordering/Ordering.Infrastructure/Queries/GetOrdersQueryHandler.cs#L15)
(ver [2.12](#212-qué-es-asnotracking-y-por-qué-las-lecturas-son-proyecciones)).

```csharp
var orderResult = Order.Place(
    command.CustomerId,
    command.CustomerEmail,
    lines,
    discountPolicy,
    timeProvider.GetUtcNow());

if (orderResult.IsFailure)
{
    return orderResult.Error;
}

orderRepository.Add(orderResult.Value);
await unitOfWork.SaveChangesAsync(cancellationToken);

return orderResult.Value.ToResponse();
```

#### 3.1.10 Test Data Builder — `OrderBuilder`

**Qué es:** un builder con **valores por defecto válidos**, para que cada test solo declare lo que le importa.
**Para qué sirve aquí:** si mañana `Order.Place` pide un parámetro más, se cambia el builder y no cien tests.

[OrderBuilder.cs](../tests/Ordering.Domain.UnitTests/Builders/OrderBuilder.cs#L11):

```csharp
internal sealed class OrderBuilder
{
    // ...
    private string _customerId = "pos-001";
    private string _customerEmail = "bar@example.com";
    private IDiscountPolicy _discountPolicy = NoDiscountPolicy.Instance;
    // ...
    public OrderBuilder WithLine(string sku, decimal unitPrice, int quantity, string currency = "USD")
    {
        _lines.Add(Line(sku, unitPrice, quantity, currency));
        return this;
    }
    // ...
}
```

### 3.2 ¿Cómo se comunican los servicios?

**Respuesta corta:** de dos formas.

1. **Asíncrona (la principal):** eventos de integración en **Azure Service Bus** con **topics y suscripciones**
   (publish/subscribe). Cada suscripción filtra por `Subject` (el nombre del evento). Se publica **siempre a través
   del outbox** y se consume **siempre de forma idempotente**.
2. **Síncrona (una sola):** Ordering → Catalog por HTTP para tomar los precios, con un pipeline de **Polly** y el
   token del usuario reenviado.

Además, el navegador habla **solo** con el Gateway (HTTP), que hace de proxy hacia los servicios.

**Dónde está en el código:** la topología completa está declarada una vez
([Topology.cs](../src/BuildingBlocks/BuildingBlocks.Contracts/Topology.cs#L36)) y el AppHost aprovisiona el
emulador desde ahí.

```csharp
/// <summary>Subscription name → topic and the event names (message Subject) it receives.</summary>
public static readonly IReadOnlyList<SubscriptionDefinition> SubscriptionDefinitions =
[
    new(Topics.OrderEvents, Subscriptions.Inventory, [nameof(OrderPlaced)]),
    new(Topics.OrderEvents, Subscriptions.Notifications, [nameof(OrderConfirmed), nameof(OrderRejected)]),
    new(Topics.InventoryEvents, Subscriptions.Ordering, [nameof(StockReserved), nameof(StockRejected)]),
];
```

Los contratos son `record`s compartidos en `BuildingBlocks.Contracts`
([OrderingEvents.cs](../src/BuildingBlocks/BuildingBlocks.Contracts/Ordering/OrderingEvents.cs#L3)):

```csharp
public sealed record OrderPlaced(
    Guid OrderId,
    string CustomerId,
    IReadOnlyList<OrderPlacedItem> Items) : IntegrationEvent;
```

Cada servicio se suscribe en su composición ([InventoryMessaging.cs](../src/Services/Inventory/Inventory.Api/Messaging/InventoryMessaging.cs#L14)):

```csharp
builder.AddServiceBusMessaging();

builder.Services.AddOutbox<InventoryDbContext>();
builder.Services.AddIntegrationEventHandler<OrderPlaced, OrderPlacedIntegrationEventHandler>();
builder.Services.AddServiceBusSubscription<InventoryDbContext>(Topology.Subscriptions.Inventory);
```

**Repreguntas típicas:** ¿Por qué topics y no colas? (Un topic permite **varios consumidores** del mismo evento:
`order-events` lo leen Inventory y Notifications, cada uno con su suscripción y su filtro. Agregar un consumidor no
toca al publicador.) ¿Por qué Inventory no se llama por HTTP? (Acoplamiento temporal: si Inventory se cae, no se
podrían hacer pedidos; ver ADR 0003.)

### 3.3 ¿Qué es el Transactional Outbox y qué problema resuelve?

**Respuesta corta:** resuelve el **dual write**: guardar el pedido y publicar `OrderPlaced` son dos operaciones en
dos sistemas. Si el proceso muere entre ellas, queda un pedido que nadie conoce o un evento de un pedido que no
existe. Con el outbox, el evento se **guarda como una fila** en la **misma transacción** que el pedido, y un proceso
en segundo plano lo publica después.

**Dónde está en el código:**

Paso 1, guardar el evento en la misma transacción: un interceptor de EF Core transforma los domain events en filas
del outbox justo antes de `SaveChanges`
([DomainEventsToOutboxInterceptor.cs](../src/Services/Ordering/Ordering.Infrastructure/Outbox/DomainEventsToOutboxInterceptor.cs#L31)):

```csharp
private static void StageDomainEvents(DbContext? dbContext)
{
    // ...
    var aggregates = dbContext.ChangeTracker
        .Entries<AggregateRoot<Guid>>()
        .Select(entry => entry.Entity)
        .Where(aggregate => aggregate.DomainEvents.Count > 0)
        .ToList();

    foreach (var aggregate in aggregates)
    {
        foreach (var domainEvent in aggregate.DomainEvents)
        {
            // CorrelationId = aggregate id (the order id): every message of one order's saga shares it.
            dbContext.AddToOutbox(IntegrationEventMapper.ToIntegrationEvent(domainEvent), aggregate.Id.ToString());
        }

        // Cleared once staged: the outbox rows are now tracked, so a second save cannot stage them twice.
        aggregate.ClearDomainEvents();
    }
}
```

Paso 2, publicar: el `OutboxProcessor` lee las filas pendientes con `FOR UPDATE SKIP LOCKED`, así varias réplicas
pueden correrlo sin publicar una fila dos veces
([OutboxProcessor.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/Outbox/OutboxProcessor.cs#L80)):

```csharp
var messages = await dbContext.Set<OutboxMessage>()
    .FromSql($"""
        SELECT * FROM outbox_messages
        WHERE processed_on_utc IS NULL AND attempts < {_options.MaxAttempts}
        ORDER BY occurred_on_utc
        LIMIT {_options.BatchSize}
        FOR UPDATE SKIP LOCKED
        """)
    .ToListAsync(cancellationToken);

foreach (var message in messages)
{
    try
    {
        await eventBus.PublishAsync(
            new OutgoingMessage(message.Id, message.Topic, message.Subject, message.Payload, message.CorrelationId, message.TraceParent),
            cancellationToken);

        message.ProcessedOnUtc = timeProvider.GetUtcNow();
        // ...
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        message.Attempts++;
        // ...
    }
}
```

**Detalle fino:** el `EventId` del domain event se reutiliza como `MessageId`, así que un guardado reintentado no
genera un segundo mensaje distinto
([IntegrationEventMapper.cs](../src/Services/Ordering/Ordering.Infrastructure/Outbox/IntegrationEventMapper.cs#L17)).

**Repreguntas típicas:** ¿El outbox garantiza *exactly once*? (No: garantiza **at-least-once**. Si el proceso muere
después de enviar y antes de marcar la fila, se reenvía. Por eso los consumidores son idempotentes.) ¿Qué
desventajas tiene? (Hasta un segundo de latencia por el polling; `LISTEN/NOTIFY` o CDC con Debezium lo eliminarían.
Y falta limpiar las filas procesadas viejas.)

### 3.4 ¿Cómo se logra que un consumidor sea idempotente (Inbox)?

**Respuesta corta:** con una tabla **inbox** con clave `(MessageId, Consumer)`. El pipeline del consumidor:
**1)** si el `MessageId` ya está en el inbox, completa el mensaje y no hace nada; **2)** ejecuta el handler, que
**no guarda**; **3)** guarda en **una sola transacción** el cambio de negocio + las filas nuevas del outbox + la fila
del inbox; **4)** completa el mensaje.

**Dónde está en el código:** [ServiceBusSubscriptionProcessor.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/Consumers/ServiceBusSubscriptionProcessor.cs#L92).

```csharp
var alreadyProcessed = await dbContext.Set<InboxMessage>()
    .AnyAsync(m => m.MessageId == messageId && m.Consumer == subscription, cancellationToken);

if (alreadyProcessed)
{
    LogDuplicate(logger, message.Subject, messageId);
    await args.CompleteMessageAsync(message, cancellationToken);
    return;
}

var context = new IntegrationEventContext(messageId, message.CorrelationId, message.DeliveryCount);
await registration.InvokeAsync(scope.ServiceProvider, integrationEvent, context, cancellationToken);

dbContext.Set<InboxMessage>().Add(new InboxMessage
{
    MessageId = messageId,
    Consumer = subscription,
    ProcessedOnUtc = timeProvider.GetUtcNow(),
});

try
{
    await dbContext.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: PostgresUniqueViolation })
{
    // A concurrent delivery of the same message may have won the race and committed first.
    // Any other unique violation is a real error: rethrow so the message is retried.
    if (!await WasProcessedAsync(messageId, cancellationToken))
    {
        throw;
    }
    // ...
}

await args.CompleteMessageAsync(message, cancellationToken);
```

Por eso el handler de Inventory no llama a `SaveChangesAsync`
([OrderPlacedIntegrationEventHandler.cs](../src/Services/Inventory/Inventory.Api/Messaging/OrderPlacedIntegrationEventHandler.cs#L30)):

```csharp
public async Task HandleAsync(OrderPlaced integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
{
    var lines = integrationEvent.Items
        .Select(item => new ReservationLine(item.Sku, item.Quantity))
        .ToArray();

    var outcome = await stockService.StageReservationAsync(lines, cancellationToken);

    // CorrelationId = order id: every message of one order's saga carries the same one.
    var correlationId = integrationEvent.OrderId.ToString();

    if (outcome.IsReserved)
    {
        outbox.Add(new StockReserved(integrationEvent.OrderId), correlationId);
        // ...
        return;
    }

    outbox.Add(
        new StockRejected(integrationEvent.OrderId, outcome.Reason!, outcome.UnavailableSkus),
        correlationId);
    // ...
}
```

**Repregunta típica:** ¿Por qué la clave incluye el `Consumer`? (El mismo `MessageId` podría ser procesado por
distintos consumidores que comparten base; cada uno lleva su registro.)

### 3.5 ¿Qué pasa con un mensaje que falla? ¿Qué es la dead-letter queue?

**Respuesta corta:** si el handler lanza una excepción, el mensaje se **abandona** y Service Bus lo reentrega. Tras
**5 entregas** (`MaxDeliveryCount`) lo mueve a la **dead-letter queue**, donde queda para inspección sin bloquear
al resto. Un mensaje ilegible va directo a la DLQ. Y un error que **nunca** se va a arreglar reintentando (el pedido
ya no está `Pending`) se **completa** y se registra en el log, en lugar de reintentarlo.

**Dónde está en el código:** la configuración de la suscripción ([AppHost.cs](../src/AppHost/AppHost.cs#L48)) y la
decisión "completar o reintentar" ([IntegrationEventFailure.cs](../src/Services/Ordering/Ordering.Infrastructure/Messaging/IntegrationEventFailure.cs#L15)).

```csharp
.WithProperties(subscription =>
{
    subscription.MaxDeliveryCount = 5;
    subscription.DeadLetteringOnMessageExpiration = true;
    // ...
```

```csharp
public static void CompleteOrThrow(ILogger logger, Error error, string eventName, Guid orderId, IntegrationEventContext context)
{
    if (error.Type == ErrorType.Conflict)
    {
        LogIgnored(logger, eventName, orderId, error.Description, context.MessageId);
        return;
    }

    throw new InvalidOperationException(
        $"{eventName} for order '{orderId}' could not be applied ({error.Code}): {error.Description}");
}
```

### 3.6 ¿Cómo está configurado Polly en la llamada a Catalog?

**Respuesta corta:** un pipeline explícito de cuatro estrategias, de afuera hacia adentro:

1. **Timeout total** (10 s): la llamada, reintentos incluidos, nunca se cuelga.
2. **Retry** (3 veces, backoff exponencial con **jitter**): para fallas transitorias (5xx, 408, 429, red).
3. **Circuit breaker** (se abre si falla el 50 % en 30 s con al menos 5 llamadas, durante 15 s): deja de golpear a
   un Catalog enfermo y **falla rápido**.
4. **Timeout por intento** (2 s): corta un intento lento para que el retry pruebe otra vez.

Lo que sobrevive al pipeline se convierte en `Catalog.Unavailable` → **503**.

**Dónde está en el código:** [CatalogClientRegistration.cs](../src/Services/Ordering/Ordering.Infrastructure/Catalog/CatalogClientRegistration.cs#L36).

```csharp
// ServiceDefaults adds the standard resilience handler to every client; remove it so handlers are never
// stacked (nested retries multiply attempts) and this pipeline is the only one in force.
#pragma warning disable EXTEXP0001 // Experimental API, but the documented way to replace the default handler.
httpClient.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

// Outside the resilience pipeline: the token is the same for every attempt.
httpClient.AddHttpMessageHandler<AccessTokenPropagationHandler>();

httpClient.AddResilienceHandler(PipelineName, static (pipeline, context) =>
{
    var options = context.ServiceProvider.GetRequiredService<IOptions<CatalogClientOptions>>().Value;

    pipeline
        .AddTimeout(options.TotalTimeout)
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = options.RetryBaseDelay,
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = options.CircuitBreakDuration,
        })
        .AddTimeout(options.AttemptTimeout);
});
```

**Repreguntas típicas:** ¿Por qué `RemoveAllResilienceHandlers()`? (ServiceDefaults ya agrega un handler estándar a
todos los clientes; dos pipelines anidados **multiplican** los reintentos.) ¿Para qué el jitter? (Para que muchos
clientes no reintenten todos en el mismo instante y tumben al servicio que se está recuperando.) ¿Se puede
reintentar un `POST`? (Aquí solo se reintenta un `GET`, que es idempotente; reintentar escrituras exige claves de
idempotencia.)

### 3.7 ¿Qué es el token relay entre Ordering y Catalog?

**Respuesta corta:** Ordering llama a Catalog **con el token del punto de venta**, no con una cuenta de servicio.
Catalog valida ese token como cualquier otro request: estar dentro de la red no es una credencial (zero trust).

**Dónde está en el código:** un `DelegatingHandler` agrega el header
([AccessTokenPropagationHandler.cs](../src/Services/Ordering/Ordering.Infrastructure/Catalog/AccessTokenPropagationHandler.cs#L10)).

```csharp
internal sealed class AccessTokenPropagationHandler(IAccessTokenProvider accessTokenProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = accessTokenProvider.GetAccessToken();

        if (request.Headers.Authorization is null && !string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
```

**Repregunta típica:** ¿Y si la llamada no viniera de un usuario (un job nocturno)? (Ahí corresponde *client
credentials*: un token propio del servicio emitido por el proveedor de identidad.)

### 3.8 ¿Cómo se evita que dos pedidos reserven el mismo último pack?

**Respuesta corta:** con **concurrencia optimista** sobre la columna de sistema **`xmin`** de PostgreSQL. EF Core
incluye `xmin` en el `WHERE` del `UPDATE`; si otra transacción cambió la fila entretanto, el `UPDATE` afecta 0
filas, `SaveChanges` falla, el mensaje se abandona y se **reintenta con las cantidades frescas**. Sin bloquear la
fila durante todo el handler.

**Dónde está en el código:** [StockItemConfiguration.cs](../src/Services/Inventory/Inventory.Api/Persistence/StockItemConfiguration.cs#L25).

```csharp
// Optimistic concurrency on PostgreSQL's xmin system column. Two OrderPlaced messages competing for the
// same SKU cannot both reserve the last packs: the second SaveChanges fails and the message is retried
// against fresh quantities (lost update prevented without locking the row for the whole handler).
stockItem.Property<uint>("Version").IsRowVersion();
```

Y como red de seguridad, un **check constraint** impide cantidades negativas
(`quantity_available >= 0 AND quantity_reserved >= 0`).

**Repregunta típica:** ¿Optimista o pesimista (`SELECT ... FOR UPDATE`)? (Optimista cuando los conflictos son raros
y reintentar es barato, como aquí gracias al broker; pesimista cuando la contención es alta o reintentar es caro.)

### 3.9 ¿Por qué no hay MediatR?

**Respuesta corta:** porque lo que aporta MediatR aquí (despachar un comando a su handler y envolverlo con
*behaviors*) son **dos interfaces y dos decorators de ~30 líneas**. Una dependencia menos, sin reflection ni
"magia" para seguir el flujo (F12 lleva al handler), y **MediatR pasó a licencia comercial**. Está escrito en el
ADR 0002.

**Explicación:**

- El controller recibe `ICommandHandler<PlaceOrderCommand, OrderResponse>` directamente. Con "ir a la
  implementación" llegas al código; con MediatR llegas a `ISender.Send` y hay que buscar el handler.
- Los decorators se arman explícitamente en `AddCommandHandler` (ver [3.1.2](#312-decorator--loggingdecorator-y-validationdecorator)).
- Cada caso de uso es **una línea** de registro: la lista funciona como índice de lo que el servicio puede hacer.

**Dónde está en el código:** [ICommandHandler.cs](../src/Services/Ordering/Ordering.Application/Abstractions/Messaging/ICommandHandler.cs#L9)
y [DependencyInjection.cs](../src/Services/Ordering/Ordering.Application/DependencyInjection.cs#L28).

```csharp
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
```

```csharp
// One line per use case: the list doubles as the index of what the service can do.
services.AddCommandHandler<PlaceOrderCommand, OrderResponse, PlaceOrderCommandHandler>();
services.AddCommandHandler<ConfirmOrderCommand, OrderStatus, ConfirmOrderCommandHandler>();
services.AddCommandHandler<RejectOrderCommand, OrderStatus, RejectOrderCommandHandler>();
```

**Cuándo sí usaría MediatR (o similar):** muchos casos de uso con varios behaviors transversales (transacciones,
caché, autorización por comando), notificaciones in-process con varios handlers, o un equipo que ya lo tiene como
estándar. Alternativas sin licencia: registrar decorators con **Scrutor**, o un mediator por source generator
(p. ej. `Mediator` de martinothamar).

### 3.10 ¿Dónde usarías Dapper?

**Respuesta corta:** en el **lado de lectura** cuando una consulta es pesada, compleja o muy frecuente y EF Core
estorba: reportes, dashboards, listados con agregaciones, SQL específico de PostgreSQL. **No** en el lado de
escritura de Ordering, donde EF Core aporta el change tracking, el interceptor del outbox, los complex types y la
concurrencia con `xmin`.

**Explicación:** hoy no hace falta, porque las lecturas ya son proyecciones `AsNoTracking` que generan un `SELECT`
mínimo. Pero el diseño lo deja preparado: los **query handlers viven en Infrastructure** detrás de `IQueryHandler<,>`,
así que reemplazar uno por Dapper **no toca Application ni el controller**. Y el propio outbox ya baja a SQL crudo
donde hace falta (`FOR UPDATE SKIP LOCKED` con `FromSql`).

**Dónde está en el código:** el candidato natural es
[GetOrdersQueryHandler.cs](../src/Services/Ordering/Ordering.Infrastructure/Queries/GetOrdersQueryHandler.cs#L10)
(ver el código en [2.12](#212-qué-es-asnotracking-y-por-qué-las-lecturas-son-proyecciones)). Una versión con Dapper
se vería así (**ejemplo hipotético, no está en el repositorio**):

```csharp
// Hypothetical: the same IQueryHandler, backed by Dapper instead of EF Core.
internal sealed class GetOrdersQueryHandler(NpgsqlDataSource dataSource)
    : IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<OrderSummaryResponse>>> HandleAsync(GetOrdersQuery query, CancellationToken cancellationToken)
    {
        // Columns in the order of the OrderSummaryResponse constructor, aliased to its parameter names.
        const string sql = """
            SELECT o.id AS Id,
                   o.status AS Status,
                   (SELECT count(*)::int FROM order_items i WHERE i.order_id = o.id) AS ItemCount,
                   o.total_amount AS Total,
                   o.total_currency AS Currency,
                   o.placed_on_utc AS PlacedOnUtc,
                   o.completed_on_utc AS CompletedOnUtc
            FROM orders o
            WHERE o.customer_id = @CustomerId
            ORDER BY o.placed_on_utc DESC
            LIMIT 100
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<OrderSummaryResponse>(
            new CommandDefinition(sql, new { query.CustomerId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
```

**Trade-offs a mencionar:** Dapper es más rápido y te da control total del SQL, pero pierdes la verificación del
compilador sobre nombres de columnas, duplicas conocimiento del esquema (si renombras una columna, la migración no
lo arregla) y tienes que testearlo contra la base real (Testcontainers). Las columnas del ejemplo son las reales
(`MoneyConfiguration` mapea `Total` a `total_amount` / `total_currency`), pero nada en el build lo verifica.

### 3.11 ¿Qué hace la Azure Function de Notifications y por qué es una Function y no un servicio?

**Respuesta corta:** es el **final de la coreografía**. Tiene dos triggers:

- **Service Bus trigger** (`OrderEvents`): recibe `OrderConfirmed` / `OrderRejected`, guarda la notificación
  (idempotente, con su inbox) y envía el e-mail.
- **HTTP trigger** (`GET /api/notifications`): lo que consulta el panel de la web.

Es una Function porque su trabajo es **reaccionar a eventos** y servir una lectura simple: no tiene reglas de negocio
complejas, no publica nada y su carga es por ráfagas. En Azure, con el plan de consumo, **escala según el largo de
la suscripción** y **a cero** cuando no hay pedidos, sin pagar por un servidor ocioso. Y el host de Functions se
encarga del **loop de recepción**, los reintentos y el dead-lettering.

**Dónde está en el código:** [OrderEventsFunction.cs](../src/Functions/Notifications/Messaging/OrderEventsFunction.cs#L18)
y [host.json](../src/Functions/Notifications/host.json).

```csharp
internal sealed class OrderEventsFunction(OrderEventHandler handler)
{
    [Function(nameof(OrderEvents))]
    public async Task OrderEvents(
        [ServiceBusTrigger(
            Topology.Topics.OrderEvents,
            Topology.Subscriptions.Notifications,
            Connection = Topology.ServiceBusConnectionName)]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        // ...
        await handler.HandleAsync(message, cancellationToken);
    }
}
```

```json
{
  "version": "2.0",
  "extensions": {
    "serviceBus": {
      "autoCompleteMessages": true,
      "maxConcurrentCalls": 4,
      "maxAutoLockRenewalDuration": "00:05:00"
    }
  }
}
```

**Diferencias con los otros servicios** (y lo que costó):

| | Servicios ASP.NET Core | Function de Notifications |
|---|---|---|
| Recepción de mensajes | `ServiceBusSubscriptionProcessor` propio | El host de Functions (trigger) |
| Outbox / `IEventBus` | Sí | No: no publica nada |
| Inbox | En el pipeline compartido | Escrito a mano en `OrderEventHandler` |
| Autenticación | `UseAuthentication()` + fallback policy | `JwtAuthenticationMiddleware` (no hay `UseAuthentication` en el pipeline de Functions) |
| Infraestructura extra | — | Storage para el host (Azurite en local) |

**Repreguntas típicas:** ¿Desventajas? (*Cold start* en el plan de consumo, un modelo de hosting distinto que el
equipo tiene que conocer, y el middleware de autenticación propio.) ¿Por qué *isolated worker*? (Es el modelo
soportado a futuro: el código corre en su propio proceso con la versión de .NET que elijas, con DI y middleware
normales.) ¿Por qué `AuthorizationLevel.Anonymous` en el HTTP trigger? (Solo apaga la *function key* del host; el
token JWT ya lo validó el middleware.)

### 3.12 ¿Qué son los BuildingBlocks y por qué están separados en cuatro proyectos?

**Respuesta corta:** son las **librerías compartidas de plomería técnica** que todos los servicios necesitan igual:
el `Result`, los contratos de eventos, la mensajería (outbox, inbox, consumidor) y la parte web (ProblemDetails,
validación, JWT, MVC). Se separan en cuatro porque **cada una arrastra dependencias distintas** y cada servicio debe
referenciar solo lo que usa.

| Proyecto | Contiene | Dependencias | Quién lo usa |
|---|---|---|---|
| `BuildingBlocks.Common` | `Result`, `Error` | **Ninguna** | Todos, incluido `Ordering.Domain` |
| `BuildingBlocks.Contracts` | Eventos de integración, `Topology` | Ninguna | Publicadores, consumidores y el AppHost |
| `BuildingBlocks.Messaging` | `IEventBus`, outbox, inbox, consumidor | EF Core, Service Bus SDK | Ordering, Inventory (y Notifications, para el inbox) |
| `BuildingBlocks.Web` | ProblemDetails, filtros de validación, JWT, `ApiControllerBase`, `IEndpoint` | ASP.NET Core, FluentValidation | Todos los servicios HTTP |

La razón más fuerte: `Ordering.Domain` **no puede** depender de EF Core ni de ASP.NET Core, pero sí necesita
`Result`. Si todo estuviera en un único "Common", el dominio arrastraría frameworks.

**Dónde está en el código:** [Ordering.Domain.csproj](../src/Services/Ordering/Ordering.Domain/Ordering.Domain.csproj).

```xml
<!--
  Innermost layer: no references to Application, Infrastructure, EF Core or ASP.NET Core.
  BuildingBlocks.Common is the only dependency (a dependency-free shared kernel with Result/Error).
-->
<ItemGroup>
  <ProjectReference Include="..\..\..\BuildingBlocks\BuildingBlocks.Common\BuildingBlocks.Common.csproj" />
</ItemGroup>
```

**El riesgo y la regla:** una librería compartida mal usada crea un **monolito distribuido** (cambias
BuildingBlocks y tienes que redesplegar todo). La regla es que ahí va **solo plomería y contratos**, nunca lógica de
negocio. En una organización grande serían paquetes NuGet versionados; aquí, con un solo repositorio, son project
references.

### 3.13 ¿Cómo se garantiza la Clean Architecture de Ordering?

**Respuesta corta:** con **tests de arquitectura** (NetArchTest) que inspeccionan los ensamblados compilados. Si
alguien agrega desde el dominio una referencia a EF Core, **falla el CI**, no una code review.

**Dónde está en el código:** [OrderingLayerTests.cs](../tests/Architecture.Tests/OrderingLayerTests.cs#L35).

```csharp
[Fact]
public void Domain_DoesNotDependOnFrameworks()
{
    var result = Types.InAssembly(_domain)
        .ShouldNot()
        .HaveDependencyOnAny(
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions",
            "Azure",
            "BuildingBlocks.Messaging",
            "BuildingBlocks.Contracts",
            "FluentValidation",
            "Riok.Mapperly")
        .GetResult();

    AssertSuccessful(result);
}
```

Las capas: `Domain ← Application ← Infrastructure ← Api`. Application define **puertos** (`ICatalogClient`,
`IUnitOfWork`, `IAccessTokenProvider`) e Infrastructure los implementa; Api es la *composition root* que conecta todo.

### 3.14 ¿Cómo se observa el sistema? ¿Cómo se sigue un pedido entre servicios?

**Respuesta corta:** con **OpenTelemetry** (trazas, métricas y logs) exportado al dashboard de Aspire. El truco
para seguir un pedido a través del bus es que el **`traceparent`** (W3C Trace Context) **viaja dentro de cada
mensaje**: el publicador lo escribe y el consumidor retoma la traza desde ahí. Un pedido es **una sola traza
distribuida**: Gateway → Ordering → Catalog → Service Bus → Inventory → Ordering → Notifications.

**Dónde está en el código:** se guarda al crear la fila del outbox
([IOutbox.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/Outbox/IOutbox.cs#L37)) y se agrega al mensaje al
publicar ([AzureServiceBusEventBus.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/ServiceBus/AzureServiceBusEventBus.cs#L25)).

```csharp
dbContext.Set<OutboxMessage>().Add(new OutboxMessage
{
    Id = integrationEvent.EventId,
    Topic = Topology.TopicFor(eventType),
    Subject = Topology.SubjectFor(eventType),
    Payload = IntegrationEventSerializer.Serialize(integrationEvent),
    CorrelationId = correlationId,
    TraceParent = Activity.Current?.Id,
    OccurredOnUtc = integrationEvent.OccurredOnUtc,
});
```

```csharp
if ((Activity.Current?.Id ?? message.TraceParent) is { } traceParent)
{
    serviceBusMessage.ApplicationProperties[MessagingDiagnostics.TraceParentProperty] = traceParent;
}
```

Además cada servicio expone `/health` y `/alive` (ServiceDefaults), y todo error HTTP lleva el `traceId`.

### 3.15 ¿Qué hace el pipeline de CI?

**Respuesta corta:** GitHub Actions, en cada push y PR a `main`, con tres jobs: **backend** (restore, build Release
con warnings como errores, tests unitarios, de arquitectura y del Gateway, luego tests de integración con
Testcontainers, y publica los `.trx`), **frontend** (`npm ci`, lint, build) y **docker** (construye las seis
imágenes, sin push).

**Dónde está en el código:** [ci.yml](../.github/workflows/ci.yml#L41).

```yaml
# Fast feedback first: everything that needs no infrastructure.
- name: Unit, architecture and functional tests
  run: |
    for project in \
      tests/Ordering.Domain.UnitTests \
      tests/Ordering.Application.UnitTests \
      tests/Inventory.UnitTests \
      tests/Notifications.UnitTests \
      tests/Architecture.Tests \
      tests/Gateway.Tests
    do
      # ...
    done

# Testcontainers starts a real PostgreSQL; the Ubuntu runner has a Docker daemon available.
- name: Integration tests
```

**Repregunta típica:** ¿Qué agregarías? (Push de imágenes a un registry, despliegue a un entorno, quality gate de
SonarQube, escaneo de dependencias y tests de contrato entre servicios.)

---
## Nivel 4 — Arquitecto

### 4.1 ¿Por qué microservicios y no un monolito?

**Respuesta corta, y honesta:** porque el objetivo del proyecto es **demostrar** las habilidades de un sistema de
microservicios (mensajería, consistencia eventual, resiliencia, seguridad entre servicios, observabilidad
distribuida) en algo lo bastante chico para explicarlo en diez minutos. Para un producto real de este tamaño, con
un solo equipo, **empezaría por un monolito modular**.

**Qué se gana con microservicios (y aquí se ve):**

- **Despliegue y escalado independientes:** Inventory puede escalar con el largo de su suscripción sin escalar
  Catalog.
- **Aislamiento de fallas:** si Inventory se cae, se siguen aceptando pedidos; quedan `Pending` y se resuelven
  cuando vuelve.
- **Propiedad de los datos:** cada servicio evoluciona su esquema sin coordinar con otros.
- **Autonomía de equipos y tecnología:** Notifications es una Function; podría estar en otro lenguaje.

**Qué se paga:** red y latencia, consistencia eventual, outbox e inbox, trazas distribuidas, más piezas para
operar, tests más difíciles y versionado de contratos. Todo eso **está en el código** justamente porque es el precio.

**Dónde está en el código:** el precio más visible es que Ordering no puede hacer un `JOIN` con el stock: tiene que
esperar un evento ([StockReservedIntegrationEventHandler.cs](../src/Services/Ordering/Ordering.Infrastructure/Messaging/StockReservedIntegrationEventHandler.cs#L14)).

```csharp
/// <summary>
/// Inbound adapter: translates the integration event into an application command. It does not save; the consumer
/// pipeline commits the order change, the <c>OrderConfirmed</c> outbox message and the inbox record together.
/// </summary>
internal sealed partial class StockReservedIntegrationEventHandler(
    ICommandHandler<ConfirmOrderCommand, OrderStatus> confirmOrder,
    ILogger<StockReservedIntegrationEventHandler> logger) : IIntegrationEventHandler<StockReserved>
{
    public async Task HandleAsync(StockReserved integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        var result = await confirmOrder.HandleAsync(new ConfirmOrderCommand(integrationEvent.OrderId), cancellationToken);
        // ...
```

### 4.2 ¿Monolito modular o microservicios? ¿Cuándo elegirías cada uno?

**Respuesta corta:** **monolito modular por defecto**; microservicios cuando haya una **razón concreta**: equipos
que se bloquean al desplegar, partes con necesidades de escala o de disponibilidad muy distintas, o requisitos de
aislamiento (seguridad, regulación). La clave es que los **límites de los módulos** sean los mismos que tendrían los
servicios, para poder extraer uno cuando haga falta.

| Criterio | Monolito modular | Microservicios |
|---|---|---|
| Equipo | Uno o pocos equipos | Varios equipos autónomos |
| Despliegue | Uno solo, simple | Independiente por servicio |
| Consistencia | Transacciones locales | Eventual (outbox, sagas) |
| Operación | Un proceso, una base (con esquemas por módulo) | Muchos procesos, red, broker, trazas distribuidas |
| Escalado | Todo junto | Por servicio |
| Costo de un límite mal elegido | Refactor dentro del mismo código | Cambiar contratos entre servicios desplegados |

**Cómo se vería este proyecto como monolito modular:** un solo host con módulos `Catalog`, `Ordering`, `Inventory`
y `Notifications`; un esquema de base por módulo; los mismos contratos de `BuildingBlocks.Contracts`, publicados en
un bus **en proceso** (o igual en Service Bus); y los mismos tests de arquitectura prohibiendo que un módulo use las
clases internas de otro. Extraer Inventory a un servicio sería cambiar el transporte, no el diseño.

**Dónde está en el código:** los límites ya están trazados por los contratos; ningún servicio referencia el código
de otro, solo `BuildingBlocks.Contracts` ([Topology.cs](../src/BuildingBlocks/BuildingBlocks.Contracts/Topology.cs#L27)).

```csharp
private static readonly Dictionary<Type, string> _topicsByEvent = new()
{
    [typeof(OrderPlaced)] = Topics.OrderEvents,
    [typeof(OrderConfirmed)] = Topics.OrderEvents,
    [typeof(OrderRejected)] = Topics.OrderEvents,
    [typeof(StockReserved)] = Topics.InventoryEvents,
    [typeof(StockRejected)] = Topics.InventoryEvents,
};
```

**Repregunta típica:** ¿Qué es un "monolito distribuido"? (Microservicios que igual hay que desplegar juntos porque
comparten base, llamadas síncronas en cadena o una librería con lógica de negocio. Se evita con base por servicio,
integración asíncrona y BuildingBlocks sin lógica de negocio.)

### 4.3 ¿Por qué YARP y qué hace?

**Respuesta corta:** **YARP** (*Yet Another Reverse Proxy*) es el reverse proxy de Microsoft, una librería para
ASP.NET Core. El Gateway lo usa para **enrutar `/api/*`** al servicio correcto, con rutas y clusters en
configuración y destinos resueltos por **service discovery**. Se eligió porque es **.NET**: mismo lenguaje, mismos
`ProblemDetails`, OpenTelemetry, health checks y tests en memoria que el resto, sin infraestructura extra en local
(ADR 0004).

**Qué hace el Gateway además del proxy:** emite los tokens de demo (`/auth/token`), valida el JWT, aplica la
política por ruta (una ruta de lectura para cualquier autenticado y otra de escritura solo para Admin), CORS y rate
limiting.

**Dónde está en el código:** el registro ([Program.cs](../src/Gateway/Program.cs#L29)) y las rutas divididas por
verbo HTTP ([appsettings.json](../src/Gateway/appsettings.json#L44)).

```csharp
// Routes, clusters and per-route policies come from configuration; destinations are Aspire service
// discovery names ("https+http://catalog"), resolved by the service discovery destination resolver.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();
```

```json
"catalog-read": {
  "ClusterId": "catalog",
  "AuthorizationPolicy": "default",
  "CorsPolicy": "web",
  "RateLimiterPolicy": "api",
  "Match": {
    "Path": "/api/products/{**catch-all}",
    "Methods": [ "GET" ]
  }
},
"catalog-admin": {
  "ClusterId": "catalog",
  "AuthorizationPolicy": "Admin",
  // ...
  "Match": {
    "Path": "/api/products/{**catch-all}",
    "Methods": [ "POST", "PUT" ]
  }
},
```

**Alternativas y cuándo:** **Azure API Management** si hacen falta productos de API, claves por suscriptor, portal
de desarrolladores y políticas gestionadas por un equipo de plataforma; **Kong, Envoy o Traefik** si la
organización ya los opera; **Ocelot** se descartó por menos activo. En producción, YARP iría detrás de **Front Door
o Application Gateway** (TLS, WAF).

### 4.4 ¿Coreografía u orquestación? ¿Por qué una saga por coreografía?

**Respuesta corta:** el pedido toca tres servicios con tres bases; no hay (ni debe haber) una transacción
distribuida. Se usa una **saga por coreografía**: cada servicio reacciona al evento anterior y emite el suyo, nadie
coordina. Con **tres pasos y sin timeouts** es lo más simple: no hay coordinador que desplegar ni mantener
disponible (ADR 0003).

**La compensación está en el diseño:** como la reserva es **todo o nada**, un pedido rechazado nunca deja reservas
parciales, así que no hay nada que deshacer. Un rechazo es un **resultado de negocio** que viaja como evento, no un
error.

**Dónde está en el código:** [Order.cs](../src/Services/Ordering/Ordering.Domain/Orders/Order.cs#L131).

```csharp
/// <summary>Inventory could not reserve the stock (compensation path of the saga).</summary>
public Result Reject(string reason, DateTimeOffset rejectedOnUtc)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(reason);

    var transition = TransitionTo(OrderStatus.Rejected, rejectedOnUtc);
    if (transition.IsFailure)
    {
        return transition;
    }

    RejectionReason = reason.Trim();
    Raise(new OrderRejectedDomainEvent(Id, CustomerId, CustomerEmail, RejectionReason, rejectedOnUtc));
    return Result.Success();
}
```

**Cuándo pasaría a orquestación** (Durable Functions o un process manager persistido): más de tres o cuatro pasos,
pasos que hay que deshacer (pago capturado y luego falla el envío), **plazos de negocio** ("rechazar si no se
reservó en 10 minutos") o la necesidad de responder "¿en qué paso está el pedido X?" desde un solo lugar.

**Debilidades a reconocer:** el flujo es implícito (hay que leer varios servicios; lo mitigan el diagrama y la traza
distribuida) y **no hay timeout**: si Inventory está caído, el pedido queda `Pending` hasta que vuelva.

### 4.5 ¿Por qué Service Bus con el SDK y no MassTransit, NServiceBus o Wolverine?

**Respuesta corta:** por tres razones (ADR 0001): **1)** que el outbox y el inbox **se vean** en dos archivos, que
es el objetivo de una demo que se explica; **2)** **licencias**: MassTransit es comercial desde la v9 y NServiceBus
también, mientras que el SDK es MIT; **3)** el **emulador** de Service Bus no soporta operaciones de administración,
y los frameworks que crean topics y suscripciones al arrancar necesitan trucos. Aquí la topología es código propio
desde el principio.

**Qué se pierde:** hay que mantener ese código (polling, reintentos, limpieza de filas procesadas) y faltan cosas que
un framework da gratis: sagas con timeouts, reentrega programada, versionado de mensajes, test harness.

**Cuándo cambiaría:** en un sistema con muchos flujos, adoptar **Wolverine** (MIT) o **MassTransit** con licencia
es lo razonable. Los contratos, la topología y los handlers idempotentes se trasladarían.

**Dónde está en el código:** el AppHost aprovisiona el emulador desde la misma `Topology` que usa el código, con un
filtro de correlación por evento ([AppHost.cs](../src/AppHost/AppHost.cs#L41)).

```csharp
foreach (var topicSubscriptions in Topology.SubscriptionDefinitions.GroupBy(s => s.Topic))
{
    var topic = serviceBus.AddServiceBusTopic(topicSubscriptions.Key);

    foreach (var definition in topicSubscriptions)
    {
        topic.AddServiceBusSubscription($"{definition.Topic}-{definition.Name}", definition.Name)
            .WithProperties(subscription =>
            {
                // ...
                // One correlation rule per event name: rules are OR-ed, so the subscription
                // only receives the events its consumer handles.
                foreach (var subject in definition.Subjects)
                {
                    subscription.Rules.Add(new AzureServiceBusRule(subject)
                    {
                        FilterType = AzureServiceBusFilterType.CorrelationFilter,
                        CorrelationFilter = new AzureServiceBusCorrelationFilter { Subject = subject },
                    });
                }
            });
    }
}
```

### 4.6 ¿Por qué cada servicio tiene un estilo de arquitectura distinto?

**Respuesta corta:** porque la **estructura debe ser proporcional a la complejidad** (ADR 0002, enmendado por el
ADR 0008). Aplicar Clean Architecture a un CRUD son cuatro proyectos de ceremonia; aplicar vertical slices a un
dominio con máquina de estados deja las reglas repartidas en handlers.

| Servicio | Complejidad | Estilo |
|---|---|---|
| Ordering | Reglas reales: snapshot de precios, descuentos, validación de líneas, máquina de estados manejada por eventos | Clean Architecture + DDD (4 proyectos) |
| Catalog | Crear, leer y actualizar productos | Vertical Slice |
| Inventory | Una regla clave (reserva todo o nada) + dos puntos de entrada (HTTP y bus) | Capa de servicio |
| Notifications | Guardar y avisar | Azure Function por responsabilidad técnica |

Además, el repositorio muestra lado a lado los tres estilos más comunes, que es justo el trade-off que se discute en
una entrevista. El costo: **tres convenciones**, que `CLAUDE.md` y los ADRs dejan por escrito.

**Dónde está en el código:** en Inventory, la regla quedó como **función pura** fuera de los handlers, así que si el
dominio crece mudarlo a su propio proyecto es mecánico
([StockReservation.cs](../src/Services/Inventory/Inventory.Api/Domain/StockReservation.cs#L3)).

```csharp
/// <summary>
/// The reservation rule of the service, written as a pure function so it can be unit tested without a database:
/// a reservation is <b>all or nothing</b>. Every line is checked first and only then is anything reserved, so a
/// partially filled order never happens and the caller gets the full list of SKUs that blocked it.
/// </summary>
public static class StockReservation
```

### 4.7 Si el Gateway ya valida el token, ¿por qué lo valida cada servicio?

**Respuesta corta:** **zero trust**. El Gateway es una comodidad, no la frontera de seguridad. Si alguien llega a la
red interna (un contenedor comprometido, un puerto expuesto por error), no debe poder llamar a Ordering sin token.
Cada servicio valida emisor, audiencia, firma y expiración con el **mismo código compartido**, y los endpoints están
cerrados por defecto.

**Dónde está en el código:** [JwtAuthenticationExtensions.cs](../src/BuildingBlocks/BuildingBlocks.Web/Authentication/JwtAuthenticationExtensions.cs#L13).

```csharp
/// <summary>
/// Validates the bearer token of every request. The Gateway already does this, but each service repeats it:
/// a service must not trust a caller just because it arrived on the internal network.
/// </summary>
// ...
bearer.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = options.Issuer,
    ValidateAudience = true,
    ValidAudience = options.Audience,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = options.CreateSigningKey(),
    ValidateLifetime = true,
    ClockSkew = options.ClockSkew,
    NameClaimType = JwtClaimNames.Subject,
    RoleClaimType = JwtClaimNames.Role,
};
```

**Repregunta típica:** ¿Por qué HS256 (clave simétrica) y no RS256? (Es una demo: una sola clave compartida es
simple. Pero **todos los que validan podrían también firmar**. En producción, RS256/ES256 con claves públicas
publicadas por el proveedor de identidad (JWKS): los servicios validan sin poder emitir.)

### 4.8 ¿Qué cambiarías para llevarlo a producción?

**Respuesta corta:** en orden de prioridad:

1. **Identidad real:** Entra ID o Keycloak emiten los tokens (RS256 + JWKS); el Gateway solo valida y enruta.
   *Client credentials* para llamadas entre máquinas.
2. **Secretos:** Azure Key Vault + Managed Identity, sin cadenas de conexión con contraseña.
3. **Infraestructura como código:** Bicep o Terraform para Service Bus, PostgreSQL Flexible Server, Container Apps o
   AKS, y Functions.
4. **Escalado:** Container Apps o Kubernetes con **KEDA** escalando consumidores por el largo de la suscripción.
5. **Borde:** Front Door o Application Gateway (TLS, WAF), rate limiting distribuido o en el borde, y quizás API
   Management.
6. **Mensajería:** limpieza de filas procesadas del outbox y del inbox, alertas sobre la DLQ, y `LISTEN/NOTIFY` o CDC
   para quitar la latencia del polling.
7. **Negocio:** liberar stock ante rechazo o cancelación y consolidarlo en el envío (tabla de reservas); timeouts
   con una saga orquestada si el flujo crece.
8. **Calidad:** tests de contrato entre servicios (Pact), SonarQube, tests de frontend, exporter de OpenTelemetry a
   Azure Monitor o Datadog.
9. **UX:** empujar el estado del pedido (SSE o SignalR) en lugar de polling.

**Dónde está en el código:** el exporter a Azure Monitor ya está marcado como punto de extensión en
[ServiceDefaults/Extensions.cs](../src/ServiceDefaults/Extensions.cs#L81).

```csharp
private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
{
    var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    if (useOtlpExporter)
    {
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
    // ...
```

### 4.9 ¿Cómo escalaría el sistema? ¿Dónde están los cuellos de botella?

**Respuesta corta:** los servicios HTTP son **sin estado** (la identidad viaja en el token), así que escalan
horizontalmente detrás de un balanceador. Los consumidores escalan por el **largo de la suscripción**. El
`OutboxProcessor` ya soporta **varias réplicas** gracias a `FOR UPDATE SKIP LOCKED`. Los límites a vigilar son:

- **Contención en el stock:** muchos pedidos del mismo SKU chocan en `xmin` y se reintentan. Soluciones:
  particionar por SKU (sesiones de Service Bus) o reservar por lotes.
- **PostgreSQL:** réplicas de lectura para las consultas; connection pooling (PgBouncer).
- **Catalog en la ruta síncrona:** caché de precios con vencimiento corto en Ordering, o un snapshot local
  alimentado por eventos `ProductPriceChanged`.
- **Rate limiter en memoria:** es por instancia; pasa a Redis o al borde.
- **Polling de la web:** a mucha escala conviene SSE o SignalR (con un backplane).

**Dónde está en el código:** el consumidor procesa hasta 4 mensajes en paralelo por instancia
([ServiceBusSubscriptionProcessor.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/Consumers/ServiceBusSubscriptionProcessor.cs#L43)).

```csharp
_processor = client.CreateProcessor(topic, subscription, new ServiceBusProcessorOptions
{
    AutoCompleteMessages = false,
    MaxConcurrentCalls = 4,
});
```

### 4.10 ¿Qué pasa si un servicio se cae a mitad del flujo?

**Respuesta corta:** depende de cuál, y en ningún caso se pierde un pedido:

| Se cae... | Qué pasa |
|---|---|
| **Catalog** | Hacer un pedido falla rápido con **503 `Catalog.Unavailable`** (retry y luego circuit breaker). Los pedidos ya hechos no se ven afectados. |
| **Inventory** | Los pedidos se aceptan y quedan `Pending`; `OrderPlaced` espera en la suscripción y se procesa cuando vuelve. |
| **Ordering después de guardar** | El evento ya está en el outbox, en la misma transacción; al volver, el `OutboxProcessor` lo publica. |
| **Service Bus** | El outbox acumula filas y reintenta; nada se pierde porque está en PostgreSQL. |
| **Notifications** | Los eventos esperan en su suscripción; el pedido ya está confirmado en Ordering. |
| **El servidor SMTP** | La notificación se guarda igual con `EmailStatus.Failed`; el panel la muestra. |

**Dónde está en el código:** el cliente de Catalog convierte lo que no sobrevivió al pipeline en un `Result`, no en
una excepción ([CatalogClient.cs](../src/Services/Ordering/Ordering.Infrastructure/Catalog/CatalogClient.cs#L46)).

```csharp
catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException or JsonException)
{
    LogCatalogUnavailable(logger, exception, exception.GetType().Name);
    return CatalogErrors.Unavailable;
}
```

### 4.11 ¿Cómo versionarías los contratos de eventos?

**Respuesta corta:** los eventos son un **contrato público**: otros servicios dependen de ellos. Las reglas:

1. **Cambios aditivos** (un campo nuevo, opcional): compatibles si los consumidores son *tolerant readers* (ignoran
   lo que no conocen).
2. **Cambios que rompen** (renombrar o quitar un campo, cambiar un tipo): un **evento nuevo** (`OrderPlacedV2`) con
   otro `Subject`, publicado en paralelo hasta que todos los consumidores migren.
3. Nunca reutilizar el significado de un campo.
4. En una organización grande: contratos en un paquete versionado o un *schema registry*, y **tests de contrato**.

**Dónde está en el código:** el cliente de Catalog ya es un *tolerant reader*: solo deserializa lo que necesita
([CatalogClient.cs](../src/Services/Ordering/Ordering.Infrastructure/Catalog/CatalogClient.cs#L53)), y el mapeo a
eventos se escribe a mano a propósito ([IntegrationEventMapper.cs](../src/Services/Ordering/Ordering.Infrastructure/Outbox/IntegrationEventMapper.cs#L8)).

```csharp
/// <summary>Only the fields Ordering needs; the rest of Catalog's response is ignored (tolerant reader).</summary>
private sealed record CatalogProductDto(string Sku, string Name, decimal Price);
```

```csharp
/// <summary>
/// Translates internal domain events into public integration contracts (1:1). Kept by hand on purpose: the
/// contract is what other services depend on, so every field that crosses the boundary is written explicitly.
/// The domain event id is reused as the message id, so saving the same event twice cannot create two messages.
/// </summary>
internal static class IntegrationEventMapper
```

Separar **domain events** (internos, pueden cambiar libremente) de **integration events** (públicos, estables) es
lo que permite refactorizar el dominio sin romper a los consumidores.

### 4.12 Si cada servicio tiene su base, ¿cómo se entera Inventory de un producto nuevo?

**Respuesta corta:** hoy **no se entera**: es una limitación reconocida del MVP. Inventory tiene su propio seeder
con los mismos 8 SKUs de Catalog, y `PUT /api/stock/{sku}` devuelve 404 para un SKU desconocido, a propósito, para
que un error de tipeo no invente un producto. Lo correcto es que Catalog publique `ProductCreated` y que Inventory
cree la fila de stock en cero al consumirlo.

**Dónde está en el código:** está marcado como TODO en
[StockService.cs](../src/Services/Inventory/Inventory.Api/Services/StockService.cs#L40).

```csharp
if (stockItem is null)
{
    // Stock is only kept for SKUs this service knows; it never creates rows from a URL, so a typo
    // cannot invent a product. TODO(phase 7+): seed new SKUs from a Catalog "product created" event.
    return StockErrors.NotFound(normalizedSku);
}
```

**Repregunta típica:** ¿Y si Inventory necesitara el nombre del producto? (Guardaría una **copia local** alimentada
por eventos, aceptando que puede estar desactualizada unos segundos. Nunca leería la base de Catalog.)

---

## Cierre: pitches, preguntas difíciles y mapa rápido

### Pitch de 1 minuto

> Es una plataforma B2B de pedidos de cerveza, inspirada en BEES, hecha en .NET 10 con microservicios. Un bar hace
> un pedido; Ordering toma los precios de Catalog, lo guarda como pendiente y publica un evento en Azure Service Bus
> a través de un transactional outbox. Inventory reserva el stock todo o nada y responde con otro evento; Ordering
> confirma o rechaza, y una Azure Function registra la notificación y manda el e-mail. Todo consumidor es
> idempotente, hay dead-letter, Polly en la única llamada síncrona, cada servicio valida el JWT, y un pedido se ve
> como una sola traza distribuida. Se levanta con un comando usando Aspire, y tiene tests unitarios, de integración
> con PostgreSQL real, de arquitectura y CI en GitHub Actions.

### Pitch de 5 minutos (guion)

1. **Problema** (30 s): pedidos B2B, no vender stock que no existe, precios del servidor.
2. **Arquitectura** (1 min): Gateway con YARP, cuatro servicios, base por servicio, Service Bus con topics
   filtrados. Mostrar el diagrama del README.
3. **El flujo** (1 min): 202 Accepted, `OrderPlaced`, reserva todo o nada, confirmación y notificación. Mostrar la
   máquina de estados de `Order`.
4. **Confiabilidad** (1 min 30 s): outbox (el interceptor y `SKIP LOCKED`), inbox (el pipeline del consumidor),
   DLQ, `xmin`, e-mail después del commit.
5. **Decisiones** (1 min): un estilo por servicio, controllers y minimal APIs, Mapperly, sin MediatR ni MassTransit.
   Todo en ADRs.
6. **Calidad** (30 s): pirámide de tests, Testcontainers, NetArchTest, CI, warnings como errores.

### Preguntas difíciles (con respuesta honesta)

**¿Esto no está sobrediseñado para un MVP de dos días?**
Para un producto, sí: empezaría por un monolito modular (ver [4.2](#42-monolito-modular-o-microservicios-cuándo-elegirías-cada-uno)).
El objetivo era demostrar, con código que funciona, los requisitos de un puesto senior de microservicios en algo
que se explica en diez minutos. Lo importante es saber **qué** es costo y **por qué** se pagó.

**¿Qué pasa con el stock reservado de un pedido que después se cancela?**
Hoy nada: las reservas **nunca se liberan** porque no hay cancelación ni envío. Está documentado como próximo paso:
liberar ante `OrderRejected` o una cancelación, y consolidar en el envío, con una tabla de reservas.

**¿Qué pasa si el pedido queda `Pending` para siempre?**
Solo si Inventory nunca vuelve o el mensaje termina en la DLQ. La coreografía no tiene un dueño natural para
"rendirse a los 5 minutos"; eso pide una saga orquestada o un job que expire pedidos viejos, más alertas sobre la
DLQ.

**¿El outbox puede publicar dos veces?**
Sí, es at-least-once por diseño (si se cae después de enviar y antes de marcar la fila). Por eso el `MessageId` es
estable (el id del domain event) y todos los consumidores tienen inbox.

**¿Por qué el precio no lo manda el frontend, si ya lo muestra?**
Porque el cliente no es confiable: podría mandar cualquier precio. El carrito muestra un **estimado**; el total real
lo calcula Ordering con el snapshot de Catalog y el descuento del servidor.

**¿Los tests de integración no son lentos?**
Más lentos que los unitarios, sí (levantan un contenedor), por eso el CI corre primero los rápidos. Pero son los
únicos que prueban lo que un mock no puede: la traducción de LINQ a SQL, los índices únicos, la atomicidad del
outbox.

**¿Qué hiciste tú y qué hizo la IA?**
El proyecto se construyó con un asistente de IA, por fases y con revisión humana al final de cada una;
[ai-workflow.md](ai-workflow.md) cuenta qué decisiones quedaron en manos humanas. Hay que poder defender cada línea:
esta guía existe para eso.

### Mapa rápido: pregunta → archivo

| Si te preguntan por... | Abre |
|---|---|
| El flujo completo | [README_es.md](../README_es.md) (diagrama), [Order.cs](../src/Services/Ordering/Ordering.Domain/Orders/Order.cs) |
| Outbox | [DomainEventsToOutboxInterceptor.cs](../src/Services/Ordering/Ordering.Infrastructure/Outbox/DomainEventsToOutboxInterceptor.cs), [OutboxProcessor.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/Outbox/OutboxProcessor.cs) |
| Inbox / idempotencia | [ServiceBusSubscriptionProcessor.cs](../src/BuildingBlocks/BuildingBlocks.Messaging/Consumers/ServiceBusSubscriptionProcessor.cs), [OrderEventHandler.cs](../src/Functions/Notifications/Messaging/OrderEventHandler.cs) |
| Topics y suscripciones | [Topology.cs](../src/BuildingBlocks/BuildingBlocks.Contracts/Topology.cs), [AppHost.cs](../src/AppHost/AppHost.cs) |
| Polly | [CatalogClientRegistration.cs](../src/Services/Ordering/Ordering.Infrastructure/Catalog/CatalogClientRegistration.cs) |
| JWT y autorización | [DemoTokenIssuer.cs](../src/Gateway/Authentication/DemoTokenIssuer.cs), [JwtAuthenticationExtensions.cs](../src/BuildingBlocks/BuildingBlocks.Web/Authentication/JwtAuthenticationExtensions.cs) |
| YARP | [Gateway/Program.cs](../src/Gateway/Program.cs), [Gateway/appsettings.json](../src/Gateway/appsettings.json) |
| E-mail | [OrderEventHandler.cs](../src/Functions/Notifications/Messaging/OrderEventHandler.cs), [SmtpEmailSender.cs](../src/Functions/Notifications/Email/SmtpEmailSender.cs), [EmailRegistration.cs](../src/Functions/Notifications/Email/EmailRegistration.cs) |
| Strategy / Null Object | [Discounts/](../src/Services/Ordering/Ordering.Domain/Discounts), [NoOpEmailSender.cs](../src/Functions/Notifications/Email/NoOpEmailSender.cs) |
| Decorators / sin MediatR | [Decorators/](../src/Services/Ordering/Ordering.Application/Decorators), [DependencyInjection.cs](../src/Services/Ordering/Ordering.Application/DependencyInjection.cs) |
| Value Objects | [ValueObjects/](../src/Services/Ordering/Ordering.Domain/ValueObjects) |
| Mapperly vs manual | [OrderMapper.cs](../src/Services/Ordering/Ordering.Application/Orders/OrderMapper.cs), [StockMapper.cs](../src/Services/Inventory/Inventory.Api/Mapping/StockMapper.cs), [ProductResponse.cs](../src/Services/Catalog/Catalog.Api/Features/Products/ProductResponse.cs) |
| Minimal APIs vs controllers | [CreateProductEndpoint.cs](../src/Services/Catalog/Catalog.Api/Features/Products/CreateProduct/CreateProductEndpoint.cs), [StockController.cs](../src/Services/Inventory/Inventory.Api/Controllers/StockController.cs), [BuildingBlocks.Web](../src/BuildingBlocks/BuildingBlocks.Web) |
| Concurrencia | [StockItemConfiguration.cs](../src/Services/Inventory/Inventory.Api/Persistence/StockItemConfiguration.cs), [OrderConfiguration.cs](../src/Services/Ordering/Ordering.Infrastructure/Persistence/Configurations/OrderConfiguration.cs) |
| Tests | [tests/](../tests), [OrderingApiFactory.cs](../tests/Ordering.IntegrationTests/Infrastructure/OrderingApiFactory.cs), [OrderingLayerTests.cs](../tests/Architecture.Tests/OrderingLayerTests.cs) |
| Decisiones | [docs/adr](adr) |
