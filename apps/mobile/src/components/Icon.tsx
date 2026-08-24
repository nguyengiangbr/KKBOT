import React from 'react';
import Icon from 'react-native-vector-icons/MaterialCommunityIcons';

interface IconProps {
  name: string;
  size?: number;
  color?: string;
  style?: any;
}

export const Icon: React.FC<IconProps> = ({ name, size = 24, color, style }) => (
  <Icon name={name} size={size} color={color} style={style} />
);

export default Icon;
