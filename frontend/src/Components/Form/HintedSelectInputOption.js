import classNames from 'classnames';
import PropTypes from 'prop-types';
import React from 'react';
import EnhancedSelectInputOption from './EnhancedSelectInputOption';
import styles from './HintedSelectInputOption.css';

function HintedSelectInputOption(props) {
  const {
    id,
    value,
    hint,
    depth,
    isSelected,
    isDisabled,
    isMultiSelect,
    isMobile,
    ...otherProps
  } = props;

  return (
    <div>
      <EnhancedSelectInputOption
        id={id}
        depth={depth}
        isSelected={isSelected}
        isDisabled={isDisabled}
        isHidden={isDisabled}
        isMultiSelect={isMultiSelect}
        isMobile={isMobile}
        {...otherProps}
      >
        <div className={classNames(
          styles.optionText,
          isMobile && styles.isMobile
        )}
        >
          <div>{value}</div>

          {
            hint != null &&
              <div className={styles.hintText}>
                {hint}
              </div>
          }
        </div>
      </EnhancedSelectInputOption>
      {
        ((props.name === "language" || props.name === "movieInfoLanguage") && id === props.minId) &&
          <div className={styles.divider}/>
      }
    </div>
  );
}

HintedSelectInputOption.propTypes = {
  id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
  value: PropTypes.string.isRequired,
  hint: PropTypes.node,
  minId: PropTypes.number,
  name: PropTypes.string,
  depth: PropTypes.number,
  isSelected: PropTypes.bool.isRequired,
  isDisabled: PropTypes.bool.isRequired,
  isMultiSelect: PropTypes.bool.isRequired,
  isMobile: PropTypes.bool.isRequired
};

HintedSelectInputOption.defaultProps = {
  isDisabled: false,
  isHidden: false,
  isMultiSelect: false
};

export default HintedSelectInputOption;
