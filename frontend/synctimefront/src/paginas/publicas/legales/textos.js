// Borradores de los documentos legales (fase 5). Pendientes de revisión por un abogado.
// La versión debe coincidir con la del backend (Negocio/Consentimientos/DocumentosLegales.cs).
export const PRIVACIDAD = {
  version: '1.0',
  titulo: 'Política de privacidad',
  vigenteDesde: '13-11-2026',
  resumen:
    'Usamos tus datos solo para responderte, cotizar y hacer tus letreros. No vendemos tus datos, no usamos rastreadores y puedes pedirnos en cualquier momento que los revisemos, corrijamos o borremos.',
  secciones: [
    {
      id: 'responsable',
      titulo: 'Quién es responsable de tus datos',
      parrafos: [
        'El responsable de tus datos personales es Letrería Los Herrera, taller de letreros ubicado en Villagrán 810, Los Ángeles, Región del Biobío, Chile.',
        'Razón social: [completar: razón social]. RUT: [completar: RUT].',
        'Para cualquier tema sobre tus datos, escríbenos a contacto.letrerosherrera@gmail.com. También puedes acercarte al taller en el horario que publicamos en este sitio.',
      ],
    },
    {
      id: 'datos-que-recogemos',
      titulo: 'Qué datos recogemos y dónde',
      parrafos: [
        'Solo pedimos los datos que necesitamos para cada trámite. Estos son los formularios y lo que guarda cada uno:',
      ],
      lista: [
        'Formulario de contacto: nombre, correo, teléfono (opcional), asunto y mensaje.',
        'Solicitud de trabajo: tus datos de contacto, la descripción del letrero que necesitas y sus medidas.',
        'Cuenta de cliente: correo y contraseña. Más adelante también podrás entrar con tu cuenta de Google.',
        'Cuentas del personal del taller: los datos necesarios para trabajar en el sistema.',
        'Acceso facial (solo dueño y administrador, opcional): un descriptor numérico cifrado de tu rostro. Nunca guardamos la foto.',
        'Consentimientos: cuándo aceptaste esta política, los términos o el acceso facial, con la versión del documento, la fecha y la hora.',
        'Pedidos: lo que encargaste, los montos, los abonos y el estado del trabajo.',
      ],
    },
    {
      id: 'para-que-los-usamos',
      titulo: 'Para qué usamos tus datos',
      parrafos: ['Usamos tus datos solo para lo siguiente:'],
      lista: [
        'Responder tus consultas. Cuando escribes, el taller recibe un aviso por correo para contestarte pronto.',
        'Revisar tus solicitudes, preparar cotizaciones y coordinar el trabajo contigo.',
        'Llevar el registro de tus pedidos, abonos y entregas.',
        'Darte acceso a tu cuenta y mantenerla segura.',
        'Permitir que el dueño y el administrador entren con su rostro, si lo eligen.',
        'Cumplir nuestras obligaciones contables y tributarias.',
        'Demostrar que diste tu consentimiento cuando la ley lo exige.',
      ],
    },
    {
      id: 'base-de-licitud',
      titulo: 'Por qué podemos usar tus datos',
      parrafos: [
        'La ley nos exige tener un motivo válido para cada uso. Nos basamos en estos tres:',
      ],
      lista: [
        'Tu consentimiento: para responder el formulario de contacto, para el acceso facial y para cualquier analítica futura. Puedes retirarlo cuando quieras, sin efecto sobre lo que hicimos antes.',
        'La ejecución de un contrato: para cotizar, hacer y entregar tu pedido, y para manejar tu cuenta.',
        'Una obligación legal: para guardar los datos de pedidos que piden las normas contables y tributarias.',
      ],
    },
    {
      id: 'dato-biometrico',
      titulo: 'El acceso facial',
      parrafos: [
        'El acceso facial es un dato biométrico y la ley lo trata como dato sensible. Por eso tiene reglas especiales.',
        'Es opcional y solo está disponible para el dueño y el administrador del taller. Los clientes no pueden ni necesitan usarlo.',
        'Solo se activa si das tu consentimiento expreso. Guardamos un descriptor numérico cifrado, que sirve para comparar tu rostro al entrar. Nunca guardamos la foto.',
        'Puedes retirar tu consentimiento cuando quieras. Al hacerlo, borramos tu descriptor y sigues entrando con tu correo y contraseña.',
      ],
    },
    {
      id: 'proveedores',
      titulo: 'Quién más recibe tus datos',
      parrafos: [
        'No vendemos ni arrendamos tus datos. Solo los comparten los proveedores que necesitamos para que el sitio funcione:',
      ],
      lista: [
        'Google (Gmail): envía los correos automáticos del sitio, como avisos y respuestas. Recibe la dirección de destino y el contenido del correo.',
        'OpenStreetMap: el mapa del taller solo se carga si tú lo activas. Al hacerlo, tu navegador pide las imágenes del mapa a los servidores de OpenStreetMap, que reciben datos técnicos como tu dirección IP. Si no lo activas, no se conecta.',
        'Alojamiento: el sitio y la base de datos están en [completar: proveedor de alojamiento y país].',
      ],
    },
    {
      id: 'conservacion',
      titulo: 'Cuánto tiempo guardamos tus datos',
      parrafos: ['Guardamos cada dato solo el tiempo necesario:'],
      lista: [
        'Pedidos: durante el plazo que exigen las normas contables y tributarias.',
        'Solicitudes rechazadas o que no se concretaron: se borran a los 12 meses.',
        'Correos automáticos ya enviados: se borran de la cola de envío a los 30 días.',
        'Consultas del formulario de contacto: [completar: plazo de conservación].',
        'Cuenta de cliente: mientras la tengas activa. Si pides la baja, la cuenta se inhabilita, borramos tu contraseña y conservamos solo el historial de pedidos que exige la ley.',
        'Acceso facial: hasta que retires tu consentimiento o se dé de baja tu cuenta del personal.',
        'Registro de consentimientos: mientras sea necesario para acreditar que lo diste.',
      ],
    },
    {
      id: 'tus-derechos',
      titulo: 'Tus derechos y cómo ejercerlos',
      parrafos: [
        'Según la Ley 19.628 y la Ley 21.719, tienes los derechos que aparecen en la lista de abajo.',
        'Para ejercerlos, escríbenos a contacto.letrerosherrera@gmail.com e indica qué derecho quieres usar. Puede que te pidamos confirmar tu identidad para proteger tus datos.',
        'Es gratis. Te responderemos dentro de 30 días corridos desde que recibamos tu solicitud, o en el plazo menor que fije la ley vigente.',
        'Si no quedas conforme con nuestra respuesta, puedes reclamar ante la Agencia de Protección de Datos Personales.',
      ],
      lista: [
        'Acceso: saber qué datos tuyos tenemos y para qué.',
        'Rectificación: corregir datos equivocados o incompletos.',
        'Supresión: pedir que borremos tus datos, salvo los que la ley nos obliga a guardar.',
        'Oposición: negarte a un uso determinado de tus datos.',
        'Portabilidad: recibir tus datos en un formato que puedas llevar a otro lado.',
        'Bloqueo: pedir que dejemos de usar tus datos por un tiempo mientras se revisa tu caso.',
      ],
    },
    {
      id: 'seguridad',
      titulo: 'Cómo cuidamos tus datos',
      parrafos: [
        'Solo el personal del taller que lo necesita para su trabajo puede ver tus datos.',
        'El descriptor del acceso facial se guarda cifrado. Tu sesión se guarda solo en la pestaña del navegador y se borra al cerrarla.',
        'Ningún sistema es infalible. Si ocurre un problema de seguridad que afecte tus datos, te avisaremos según lo que exige la ley.',
      ],
    },
    {
      id: 'cookies',
      titulo: 'Cookies y almacenamiento en tu navegador',
      parrafos: [
        'Hoy no usamos analítica ni rastreadores. Solo guardamos en tu navegador lo técnicamente necesario para que el sitio funcione (ver la lista de abajo).',
        'El aviso de cookies te deja aceptar o rechazar una analítica que podríamos usar en el futuro. Viene desmarcada y solo se activaría si la aceptas. Puedes cambiar tu elección cuando quieras desde el mismo aviso.',
      ],
      lista: [
        'Tu sesión, en sessionStorage. Se borra al cerrar la pestaña.',
        'Tu elección sobre cookies, en localStorage, para no preguntarte cada vez.',
      ],
    },
    {
      id: 'menores-de-edad',
      titulo: 'Menores de edad',
      parrafos: [
        'Este sitio está pensado para personas adultas y negocios. Si eres menor de 18 años, pide a tu madre, padre o tutor que haga la consulta o la solicitud por ti.',
        'Si sabemos que recibimos datos de un menor sin autorización, los borraremos.',
      ],
    },
    {
      id: 'cambios-y-versiones',
      titulo: 'Cambios a esta política',
      parrafos: [
        'Cada versión de esta política tiene un número y una fecha. Registramos qué versión aceptaste y cuándo.',
        'Si cambiamos algo importante, publicaremos la nueva versión y te pediremos aceptarla de nuevo.',
      ],
    },
  ],
};

export const TERMINOS = {
  version: '1.0',
  titulo: 'Términos y condiciones',
  vigenteDesde: '13-11-2026',
  resumen:
    'Estas son las reglas para usar el sitio y encargar letreros al taller. Los precios de la vitrina son referenciales, no hay pago en línea y tus derechos como consumidor siempre se respetan.',
  secciones: [
    {
      id: 'quien-presta-el-servicio',
      titulo: 'Quién presta el servicio',
      parrafos: [
        'El servicio lo presta Letrería Los Herrera, taller de letreros ubicado en Villagrán 810, Los Ángeles, Región del Biobío, Chile.',
        'Razón social: [completar: razón social]. RUT: [completar: RUT]. Correo: contacto.letrerosherrera@gmail.com.',
        'Al usar el sitio aceptas estos términos. Si no estás de acuerdo, por favor no lo uses.',
      ],
    },
    {
      id: 'uso-del-sitio',
      titulo: 'Uso del sitio',
      parrafos: [
        'En el sitio puedes ver nuestros trabajos, el horario, saber si el local está abierto, escribirnos y enviar solicitudes de trabajo.',
        'Te pedimos usarlo de buena fe. No está permitido:',
      ],
      lista: [
        'Enviar datos falsos o de otra persona sin su permiso.',
        'Enviar mensajes ofensivos, publicidad no pedida o contenido ilegal.',
        'Intentar entrar a cuentas ajenas o dañar el funcionamiento del sitio.',
      ],
    },
    {
      id: 'solicitudes-y-cotizaciones',
      titulo: 'Solicitudes y cotizaciones',
      parrafos: [
        'Los precios que ves en la vitrina son referenciales. Sirven para darte una idea, pero no son una oferta final.',
        'El precio final lo fija la cotización, que depende de las medidas, los materiales y si el letrero necesita instalación.',
        'Enviar una solicitud no te obliga a nada. El encargo queda confirmado solo cuando aceptas la cotización.',
      ],
    },
    {
      id: 'pedidos-y-pagos',
      titulo: 'Pedidos, abonos y pagos',
      parrafos: [
        'El sitio no recibe pagos en línea. Pagas en el taller o por transferencia bancaria.',
        'Para empezar un trabajo puede pedirse un abono de [completar: porcentaje o monto del abono]. El saldo se paga [completar: momento del pago del saldo].',
        'Los datos para transferir te los entregamos en el taller o desde contacto.letrerosherrera@gmail.com. Si alguien te pide pagar por otro medio a nombre del taller, consúltanos antes.',
      ],
    },
    {
      id: 'plazos-y-entregas',
      titulo: 'Plazos y entregas',
      parrafos: [
        'El plazo de entrega se indica en la cotización y empieza a correr cuando se confirma el pedido y, si corresponde, se paga el abono.',
        'Si necesitamos más tiempo por una causa que no depende de nosotros, te avisaremos lo antes posible y acordaremos una nueva fecha contigo.',
        'Si el letrero incluye instalación, coordinaremos contigo el día y la hora.',
      ],
    },
    {
      id: 'logotipos-y-material',
      titulo: 'Logotipos y material del cliente',
      parrafos: [
        'Cuando nos entregas un logo, imagen o texto para tu letrero, declaras que tienes derecho a usarlo.',
        'Usamos ese material solo para hacer tu trabajo.',
        'Solo mostramos en la vitrina trabajos con tu logo si nos das tu autorización. Puedes pedirnos que los retiremos cuando quieras.',
      ],
    },
    {
      id: 'garantia',
      titulo: 'Garantía y tus derechos como consumidor',
      parrafos: [
        'Estos términos no limitan los derechos que te da la Ley 19.496 sobre protección de los derechos de los consumidores.',
        'Si tu letrero presenta una falla de fabricación, tienes la garantía legal: puedes pedir que lo reparemos, lo cambiemos o te devolvamos lo pagado, dentro de los 6 meses siguientes a la entrega.',
        'La garantía no cubre daños por mal uso, golpes, modificaciones hechas por terceros o desgaste normal.',
      ],
    },
    {
      id: 'cuentas',
      titulo: 'Tu cuenta y tu contraseña',
      parrafos: [
        'Eres responsable de cuidar tu contraseña y de lo que se haga con tu cuenta. No la compartas.',
        'Si crees que alguien entró a tu cuenta, cambia tu contraseña y escríbenos.',
        'Puedes pedir la baja de tu cuenta cuando quieras. La cuenta se inhabilita y se borra tu contraseña, pero conservamos el historial de pedidos que exige la ley.',
      ],
    },
    {
      id: 'ley-aplicable',
      titulo: 'Ley aplicable y tribunales',
      parrafos: [
        'Estos términos se rigen por las leyes de Chile.',
        'Cualquier conflicto se verá ante los tribunales de Los Ángeles, sin perjuicio de tu derecho como consumidor a acudir al tribunal de tu domicilio o al Servicio Nacional del Consumidor (SERNAC).',
      ],
    },
    {
      id: 'contacto',
      titulo: 'Contacto',
      parrafos: [
        'Para dudas sobre estos términos, escríbenos a contacto.letrerosherrera@gmail.com o visítanos en Villagrán 810, Los Ángeles.',
        'Cada versión de estos términos tiene un número y una fecha. Si cambiamos algo importante, te pediremos aceptarla de nuevo.',
      ],
    },
  ],
};
