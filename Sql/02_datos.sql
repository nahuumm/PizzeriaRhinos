-- =========================================================================
-- PROYECTO FINAL: SISTEMA POS Y ADMINISTRACIÓN PARA PIZZERÍA (RHINOS)
-- ARCHIVO: 02_datos.sql
-- DESCRIPCIÓN: Datos iniciales maestros y de prueba reproducibles
-- =========================================================================

USE pizzeria_rhinos;

-- 1. EMPLEADOS (Solo Admin, Cajeras y Repartidores; sin cuentas de cocina)
INSERT INTO EMPLEADO (nombre, rol, usuario, contrasena, activo) VALUES
('Administrador Rhinos', 'ADMIN', 'admin', 'admin123', TRUE),
('Cajera 1 - Ana López', 'CAJERO', 'cajera1', 'caja123', TRUE),
('Cajera 2 - Sofía Ruiz', 'CAJERO', 'cajera2', 'caja123', TRUE),
('Repartidor 1 - Carlos Méndez', 'REPARTIDOR', NULL, NULL, TRUE),
('Repartidor 2 - Luis Torres', 'REPARTIDOR', NULL, NULL, TRUE),
('Repartidor 3 - Jorge Ramírez', 'REPARTIDOR', NULL, NULL, TRUE);

-- 2. CLIENTES FRECUENTES (Con dirección completa y referencias)
INSERT INTO CLIENTE (telefono, nombre, calle, numero_casa, colonia, entre_calles, referencias) VALUES
('6461112233', 'María Fernanda Castro', 'Av. Reforma', '1420', 'Valle Dorado', 'Entre Calle 14 y Calle 15', 'Casa blanca de dos pisos con reja negra'),
('6462223344', 'Roberto Sánchez', 'Calle Tercera', '518', 'Zona Centro', 'Entre Ruiz y Gastélum', 'Portón café, camioneta pick-up roja afuera'),
('6463334455', 'Diana Morales', 'Blvd. Costero', '890-B', 'Playa Ensenada', 'Entre Sanginés y Las Dunas', 'Departamento planta baja junto a tienda de abarrotes'),
('6464445566', 'Héctor Navarro', 'Calle Delante', '305', 'Carlos Pacheco', 'Entre Espinoza y Riveroll', 'Casa amarilla esquina con árbol grande');

-- 3. TAMAÑOS Y PRESENTACIONES (Solo Mediana, Grande y Extragrande aplican 2x1)
INSERT INTO TAMANO_PRESENTACION (id_tamano, nombre, aplica_promocion_2x1) VALUES
(1, 'Personal', FALSE),
(2, 'Chica', FALSE),
(3, 'Mediana', TRUE),
(4, 'Grande', TRUE),
(5, 'Extragrande', TRUE),
(6, 'Rino', FALSE),
(7, 'Mega Rino', FALSE),
(8, 'Jumbo Rino', FALSE),
(9, 'Orden', FALSE),
(10, '1/2 Kg', FALSE),
(11, 'Lata 355 ml', FALSE),
(12, '600 ml', FALSE),
(13, '1 Litro', FALSE),
(14, '2 Litros', FALSE);

-- 4. INGREDIENTES (Catálogo base sin gramaje)
INSERT INTO INGREDIENTE (id_ingrediente, nombre, disponible) VALUES
(1, 'Pepperoni', TRUE),
(2, 'Jamón', TRUE),
(3, 'Piña', TRUE),
(4, 'Champiñones', TRUE),
(5, 'Cebolla', TRUE),
(6, 'Pimiento Verde', TRUE),
(7, 'Aceitunas Negras', TRUE),
(8, 'Salchicha Italiana', TRUE),
(9, 'Chorizo', TRUE),
(10, 'Tocino', TRUE),
(11, 'Jalapeño', TRUE),
(12, 'Carne Molida', TRUE),
(13, 'Pollo BBQ', TRUE),
(14, 'Salami', TRUE),
(15, 'Tomate', TRUE),
(16, 'Frijoles', TRUE);

-- 5. PRODUCTOS (16 Especialidades de Pizza + Mostrador + Calzone + Complementos + Bebidas)
INSERT INTO PRODUCTO (id_producto, nombre, categoria, max_ingredientes_base, disponible) VALUES
-- 16 Especialidades de Pizza
(1, 'Pepperoni Clásica', 'PIZZA', 5, TRUE),
(2, 'Hawaiana', 'PIZZA', 5, TRUE),
(3, 'Mexicana', 'PIZZA', 5, TRUE),
(4, 'Suprema Rhinos', 'PIZZA', 5, TRUE),
(5, 'Carnes Frías', 'PIZZA', 5, TRUE),
(6, 'Vegetariana', 'PIZZA', 5, TRUE),
(7, 'Cuatro Quesos', 'PIZZA', 5, TRUE),
(8, 'Pollo BBQ', 'PIZZA', 5, TRUE),
(9, 'Ranchera', 'PIZZA', 5, TRUE),
(10, 'Italiana', 'PIZZA', 5, TRUE),
(11, 'Norteña', 'PIZZA', 5, TRUE),
(12, 'Champiñones y Pepperoni', 'PIZZA', 5, TRUE),
(13, 'Tocino Especial', 'PIZZA', 5, TRUE),
(14, 'Al Pastor', 'PIZZA', 5, TRUE),
(15, 'Tres Carnes', 'PIZZA', 5, TRUE),
(16, 'Especial de la Casa', 'PIZZA', 5, TRUE),
-- Pizza Exclusiva de Mostrador (1 ingrediente)
(17, 'Pizza Mostrador 1 Ingrediente (Pepperoni)', 'PIZZA_MOSTRADOR', 1, TRUE),
-- Calzone y Complementos
(18, 'Calzone Especial', 'CALZONE', 5, TRUE),
(19, 'Alitas BBQ', 'COMPLEMENTO', 0, TRUE),
(20, 'Boneless Bufalo', 'COMPLEMENTO', 0, TRUE),
(21, 'Papas Gajo', 'COMPLEMENTO', 0, TRUE),
(22, 'Pan de Ajo con Queso', 'COMPLEMENTO', 0, TRUE),
-- Bebidas (Control automático de stock)
(23, 'Coca-Cola', 'BEBIDA', 0, TRUE),
(24, 'Sprite', 'BEBIDA', 0, TRUE),
(25, 'Fanta Naranja', 'BEBIDA', 0, TRUE),
(26, 'Manzanita Sol', 'BEBIDA', 0, TRUE);

-- 6. RECETAS BASE (Máximo 5 ingredientes por especialidad para probar regla del 6.° ingrediente)
INSERT INTO RECETA_PRODUCTO (id_producto, id_ingrediente) VALUES
-- 1. Pepperoni Clásica (1 ing)
(1, 1),
-- 2. Hawaiana (2 ing)
(2, 2), (2, 3),
-- 3. Mexicana (4 ing)
(3, 9), (3, 5), (3, 11), (3, 15),
-- 4. Suprema Rhinos (5 ing -> ya está al límite de 5, el siguiente agregado se cobra)
(4, 1), (4, 8), (4, 4), (4, 5), (4, 6),
-- 5. Carnes Frías (4 ing)
(5, 1), (5, 2), (5, 8), (5, 14),
-- 6. Vegetariana (5 ing -> al límite de 5)
(6, 4), (6, 5), (6, 6), (6, 7), (6, 15),
-- 8. Pollo BBQ (3 ing)
(8, 13), (8, 5), (8, 10),
-- 9. Ranchera (4 ing)
(9, 9), (9, 16), (9, 5), (9, 11),
-- 12. Champiñones y Pepperoni (2 ing)
(12, 1), (12, 4),
-- 15. Tres Carnes (3 ing)
(15, 1), (15, 2), (15, 10),
-- 16. Especial de la Casa (5 ing)
(16, 1), (16, 2), (16, 4), (16, 5), (16, 7),
-- 17. Pizza Mostrador (1 ing)
(17, 1),
-- 18. Calzone Especial (3 ing)
(18, 1), (18, 2), (18, 4);

-- 7. PRODUCTO_PRECIO (Tarifas para todas las 16 especialidades en los 8 tamaños de pizza)
INSERT INTO PRODUCTO_PRECIO (id_producto, id_tamano, variante, precio, stock_actual)
SELECT p.id_producto, t.id_tamano, 'NORMAL',
    CASE t.id_tamano
        WHEN 1 THEN 89.00   -- Personal
        WHEN 2 THEN 129.00  -- Chica
        WHEN 3 THEN 179.00  -- Mediana (2x1)
        WHEN 4 THEN 219.00  -- Grande (2x1)
        WHEN 5 THEN 259.00  -- Extragrande (2x1)
        WHEN 6 THEN 310.00  -- Rino
        WHEN 7 THEN 360.00  -- Mega Rino
        WHEN 8 THEN 420.00  -- Jumbo Rino
    END,
    NULL
FROM PRODUCTO p
CROSS JOIN TAMANO_PRESENTACION t
WHERE p.categoria = 'PIZZA' AND t.id_tamano BETWEEN 1 AND 8;

-- Tarifas exclusivas de Pizza Mostrador (Solo Grande y Extragrande)
INSERT INTO PRODUCTO_PRECIO (id_producto, id_tamano, variante, precio, stock_actual) VALUES
(17, 4, 'MOSTRADOR', 139.00, NULL),
(17, 5, 'MOSTRADOR', 169.00, NULL);

-- Tarifas de Calzone y Complementos
INSERT INTO PRODUCTO_PRECIO (id_producto, id_tamano, variante, precio, stock_actual) VALUES
(18, 9, 'NORMAL', 115.00, NULL),  -- Calzone Orden
(19, 9, 'NORMAL', 125.00, NULL),  -- Alitas Orden
(19, 10, 'NORMAL', 185.00, NULL), -- Alitas 1/2 Kg
(20, 9, 'NORMAL', 130.00, NULL),  -- Boneless Orden
(20, 10, 'NORMAL', 190.00, NULL), -- Boneless 1/2 Kg
(21, 9, 'NORMAL', 65.00, NULL),   -- Papas Gajo Orden
(22, 9, 'NORMAL', 55.00, NULL);   -- Pan de Ajo Orden

-- Tarifas y Stock Inicial de Bebidas (Stock controlado automáticamente)
INSERT INTO PRODUCTO_PRECIO (id_producto, id_tamano, variante, precio, stock_actual) VALUES
(23, 11, 'NORMAL', 22.00, 24), -- Coca-Cola Lata 355ml
(23, 12, 'NORMAL', 28.00, 20), -- Coca-Cola 600ml
(23, 13, 'NORMAL', 36.00, 12), -- Coca-Cola 1L
(23, 14, 'NORMAL', 48.00, 15), -- Coca-Cola 2L
(24, 11, 'NORMAL', 22.00, 18), -- Sprite Lata
(24, 12, 'NORMAL', 28.00, 15), -- Sprite 600ml
(24, 14, 'NORMAL', 45.00, 10), -- Sprite 2L
(25, 12, 'NORMAL', 28.00, 12), -- Fanta 600ml
(25, 14, 'NORMAL', 45.00, 8),  -- Fanta 2L
(26, 12, 'NORMAL', 28.00, 2);  -- Manzanita 600ml (Stock bajo para probar agotamiento)

-- 8. PRECIO_INGREDIENTE_EXTRA (Costo del 6.° ingrediente en adelante según tamaño de pizza)
INSERT INTO PRECIO_INGREDIENTE_EXTRA (id_ingrediente, id_tamano, precio_extra)
SELECT i.id_ingrediente, t.id_tamano,
    CASE t.id_tamano
        WHEN 1 THEN 12.00
        WHEN 2 THEN 15.00
        WHEN 3 THEN 20.00
        WHEN 4 THEN 25.00
        WHEN 5 THEN 30.00
        WHEN 6 THEN 35.00
        WHEN 7 THEN 40.00
        WHEN 8 THEN 45.00
    END
FROM INGREDIENTE i
CROSS JOIN TAMANO_PRESENTACION t
WHERE t.id_tamano BETWEEN 1 AND 8;

-- 9. TARIFA_OPCION_FINAL (Orilla de queso, queso extra y masa de sartén por tamaño)
INSERT INTO TARIFA_OPCION_FINAL (id_tamano, tipo_opcion, precio_adicional) VALUES
(1, 'ORILLA_QUESO', 20.00), (1, 'QUESO_EXTRA', 15.00), (1, 'MASA_SARTEN', 15.00),
(2, 'ORILLA_QUESO', 25.00), (2, 'QUESO_EXTRA', 20.00), (2, 'MASA_SARTEN', 20.00),
(3, 'ORILLA_QUESO', 35.00), (3, 'QUESO_EXTRA', 25.00), (3, 'MASA_SARTEN', 25.00),
(4, 'ORILLA_QUESO', 45.00), (4, 'QUESO_EXTRA', 35.00), (4, 'MASA_SARTEN', 30.00),
(5, 'ORILLA_QUESO', 55.00), (5, 'QUESO_EXTRA', 45.00), (5, 'MASA_SARTEN', 35.00),
(6, 'ORILLA_QUESO', 65.00), (6, 'QUESO_EXTRA', 50.00), (6, 'MASA_SARTEN', 40.00),
(7, 'ORILLA_QUESO', 75.00), (7, 'QUESO_EXTRA', 60.00), (7, 'MASA_SARTEN', 45.00),
(8, 'ORILLA_QUESO', 85.00), (8, 'QUESO_EXTRA', 70.00), (8, 'MASA_SARTEN', 50.00);

-- 10. PEDIDOS HISTÓRICOS DE PRUEBA (Para probar historial de cliente y corte de caja)
INSERT INTO PEDIDO (id_pedido, id_cliente, id_cajero, id_repartidor, fecha_hora, tipo_servicio, subtotal, total_extras, total, estado) VALUES
(1, 1, 2, 4, NOW() - INTERVAL 2 DAY, 'DOMICILIO', 219.00, 45.00, 264.00, 'ENTREGADO'),
(2, NULL, 2, NULL, NOW() - INTERVAL 1 DAY, 'MOSTRADOR', 139.00, 0.00, 139.00, 'ENTREGADO');

INSERT INTO DETALLE_PEDIDO (id_detalle, id_pedido, id_producto_precio, id_producto_mitad2, cantidad, tipo_masa, con_orilla_queso, con_queso_extra, es_promocion_2x1, grupo_promo, subtotal_linea)
VALUES
(
    1,
    1,
    (SELECT id_producto_precio FROM PRODUCTO_PRECIO WHERE id_producto = 4 AND id_tamano = 4 AND variante = 'NORMAL' LIMIT 1),
    2,
    1, 'GRUESA', TRUE, FALSE, FALSE, NULL, 264.00
),
(
    2,
    2,
    (SELECT id_producto_precio FROM PRODUCTO_PRECIO WHERE id_producto = 17 AND id_tamano = 4 AND variante = 'MOSTRADOR' LIMIT 1),
    NULL,
    1, 'GRUESA', FALSE, FALSE, FALSE, NULL, 139.00
);