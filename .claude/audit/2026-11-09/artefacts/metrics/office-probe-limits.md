# Limites de sondes Office

Une première commande regroupait CEILING, FLOOR, REPT et SUBSTITUTE. Le cas `REPT("",2147483648)`, choisi avec une sortie théoriquement vide, a commencé une itération longue. La commande est revenue avec une session encore active après dix secondes et a été interrompue immédiatement par Ctrl+C. Ce cas est une sonde interrompue, pas une exception prouvée et pas un test terminé. Aucune répétition de ce cas sur l’hôte. Les affectations initiales à la variable PowerShell réservée `$Error` ont aussi été refusées ; ces résultats initiaux ne servent pas de preuve.

La commande suivante, séparée, n’a exécuté que CEILING et FLOOR à coût constant, avec une variable dédiée. Ses deux OverflowException sont dans office-formula-cast-proof.json. Aucune sonde SUBSTITUTE n’est revendiquée.

Toute confirmation ultérieure de coût doit être bornée dans le conteneur strict prescrit par l’orchestrateur, sans réseau, un CPU, 256 Mo et timeout. Cette confirmation n’est pas effectuée dans cette passe.
