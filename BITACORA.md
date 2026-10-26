\# Bitácora de desarrollo — BattleHub Game Typing



\## 1. Información general



\- \*\*Proyecto:\*\* BattleHub Game Typing

\- \*\*Repositorio:\*\* https://github.com/Johanaaguero/battlehub-game-typing

\- \*\*Integrante:\*\* Wayner Díaz Mora

\- \*\*Rol:\*\* Implementación del juego y modo de práctica individual

\- \*\*Rama de trabajo:\*\* `feature/wayner-practica-individual`

\- \*\*Fecha de actualización:\*\* 2026-10-25



\## 2. Objetivo de la bitácora



Registrar las actividades realizadas por los integrantes del equipo, los estados de las tareas, las evidencias de implementación y pruebas, y los pendientes del proyecto.



Cada integrante debe actualizar sus actividades con información verificable. La bitácora será revisada y consolidada para mantener un registro uniforme del avance del equipo.



\## 3. Estados de las actividades



\- \*\*In Process:\*\* actividad iniciada que está pendiente de una dependencia, comprobación o resolución de un bloqueo.

\- \*\*Doing:\*\* actividad que se está realizando activamente.

\- \*\*Done:\*\* actividad completada y validada con evidencia disponible.



Se utilizarán estos estados de manera consistente con las indicaciones del profesor.



\## 4. Registro de actividades — Wayner Díaz Mora



| Fecha | Integrante | Actividad | Estado | Evidencia | Observaciones |

|---|---|---|---|---|---|

| 2026-10-25 | Wayner Díaz Mora | Implementación del modo de práctica individual de Typing Battle. | Done | Commit `c5b2099`. | La práctica puede ejecutarse localmente sin iniciar una partida real desde Shell. |

| 2026-10-25 | Wayner Díaz Mora | Incorporación de pruebas automatizadas para el modo individual y el flujo integrado mediante mocks. | Done | Archivo `frontend/test/game-practice.spec.ts`. | Se agregaron pruebas para inicio, métricas, finalización, tiempo agotado, reinicio y flujo integrado simulado. |

| 2026-10-25 | Wayner Díaz Mora | Actualización de la interfaz para mostrar el modo de práctica individual. | Done | Archivo `frontend/src/game-module.html`. | Se agregó la opción para iniciar la práctica y visualizar sus resultados. |

| 2026-10-25 | Wayner Díaz Mora | Incorporación del componente del juego a la pantalla local de la aplicación. | Done | Archivo `frontend/src/my-app.html`. | Se comprobó que Typing Battle se visualiza en el navegador local. |

| 2026-10-25 | Wayner Díaz Mora | Ejecución de las pruebas automatizadas y del lint. | Done | `npm test -- --runInBand`: 19 pruebas aprobadas localmente. | La última ejecución reportada mostró 3 suites aprobadas y 19 pruebas aprobadas. |

| 2026-10-25 | Wayner Díaz Mora | Compilación de producción. | Done | `npm run build`. | Webpack compiló correctamente antes de la última corrección de la prueba de la aplicación. |

| 2026-10-25 | Wayner Díaz Mora | Corrección de la prueba de la pantalla principal. | Done | Commit `64af3e9`. | Se actualizó `frontend/test/my-app.spec.ts` para comprobar el mensaje dentro de `.message`. Las pruebas locales volvieron a pasar. |

| 2026-10-25 | Wayner Díaz Mora | Publicación de los cambios en GitHub. | Done | Rama `feature/wayner-practica-individual`. | Los commits `c5b2099` y `64af3e9` fueron publicados en la rama remota. |

| 2026-10-25 | Wayner Díaz Mora | Revisión del Pull Request. | Done | Pull Request de la rama de práctica individual. | El integrante informa que una compañera aceptó el Pull Request después de corregir la prueba fallida. |

| 2026-10-25 | Wayner Díaz Mora | Verificación de la integración real con Shell, Auth0 y SignalR. | In Process | Prueba local del Shell. | Se observó un error de conexión al servicio de perfiles. La integración real todavía debe verificarse. |



\## 5. Resumen del avance de Wayner Díaz Mora



\### Actividades completadas



\- Implementación del modo de práctica individual.

\- Inicio de la práctica, escritura y cálculo de métricas.

\- Finalización local y opción para volver a jugar.

\- Incorporación de pruebas automatizadas.

\- Corrección de la prueba de la pantalla principal.

\- Publicación de los cambios en la rama de trabajo.

\- Ejecución satisfactoria de las pruebas locales.



\### Pendientes



\- Verificar la integración real con Shell.

\- Comprobar la autenticación mediante Auth0 en el flujo integrado.

\- Confirmar la conexión real con SignalR y la recepción del resultado oficial.

\- Revisar el estado final de las comprobaciones automáticas de GitHub después de la corrección.



\## 6. Registro de actividades del resto del equipo



Cada integrante debe completar su propia fila con la actividad realizada y una evidencia verificable.



| Fecha | Integrante | Actividad | Estado | Evidencia | Observaciones |

|---|---|---|---|---|---|

| Por completar | Johana Agüero Serrano | Por registrar por el integrante. | In Process | Por completar. | Actualizar según el avance real. |

| Por completar | Joan García Briceño | Por registrar por el integrante. | In Process | Por completar. | Actualizar según el avance real. |

| Por completar | Dallene Blandón Cubillo | Por registrar por el integrante. | In Process | Por completar. | Actualizar según el avance real. |



\## 7. Criterios para mantener la bitácora



1\. Cada integrante registra sus actividades y actualiza los estados correspondientes.

2\. Las actividades marcadas como Done deben contar con evidencia.

3\. Los commits, Pull Requests y resultados de pruebas deben anotarse cuando correspondan.

4\. Los errores y bloqueos deben registrarse sin ocultarlos.

5\. La persona coordinadora revisa que no existan duplicados ni contradicciones.

6\. La bitácora debe mantenerse actualizada durante el desarrollo del proyecto.

